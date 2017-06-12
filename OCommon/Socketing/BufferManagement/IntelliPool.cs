using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace OceanChip.Common.Socketing.BufferManagement
{
    struct PoolItemState
    {
        public byte Generation { get; set; }
    }
    public class IntelliPool<T>:IntelliPoolBase<T>
    {
        private ConcurrentDictionary<T, PoolItemState> _bufferDict = new ConcurrentDictionary<T, PoolItemState>();
        private ConcurrentDictionary<T, T> _removeItemDict;

        public IntelliPool(int initialCount,IPoolItemCreator<T> itemCreator,Action<T> itemCleaner=null,Action<T> itemPreGet = null):
            base(initialCount,itemCreator,itemCleaner,itemPreGet)
        {

        }
        protected override void RegisterNewItem(T item)
        {
            PoolItemState state = new PoolItemState();
            state.Generation = CurrentGeneration;
            _bufferDict.TryAdd(item, state);
        }
        public override bool Shrink()
        {
            var generation = CurrentGeneration;
            if (!base.Shrink())
                return false;
            var toReRemoved = new List<T>(TotalCount / 2);
            foreach(var item in _bufferDict)
            {
                if (item.Value.Generation == generation)
                    toReRemoved.Add(item.Key);
            }
            if (_removeItemDict == null)
                _removeItemDict = new ConcurrentDictionary<T, T>();

            foreach(var item in toReRemoved)
            {
                PoolItemState state;
                if (_bufferDict.TryRemove(item, out state))
                    _removeItemDict.TryAdd(item, item);
            }
            return true;
        }
        protected override bool CanReturn(T item)
        {
            return _bufferDict.ContainsKey(item);
        }
        protected override bool TryRemove(T item)
        {
            if (_removeItemDict == null || _removeItemDict.Count == 0)
                return false;
            T removedItem;
            return _removeItemDict.TryRemove(item, out removedItem);
        }
    }
    public abstract class IntelliPoolBase<T> : IPool<T>
    {
        private ConcurrentStack<T> _store;
        private IPoolItemCreator<T> _itemCreator;
        private byte _currentGeneration = 0;
        private int _nextExpandThreshold;
        private int _totalCount;
        private int _avaliableCunt;
        private int _inExpanding = 0;
        private Action<T> _itemCleaner;
        private Action<T> _itemPreGet;

        protected byte CurrentGeneration => _currentGeneration;
        public int TotalCount => _totalCount;
        public int AvailableCount => _avaliableCunt;

        public IntelliPoolBase(int initialCount,IPoolItemCreator<T> itemCreator,Action<T> itemCleaner=null,Action<T> itemPreGet=null)
        {
            this._itemCreator = itemCreator;
            this._itemCleaner = itemCleaner;
            this._itemPreGet = itemPreGet;

            var list = new List<T>(initialCount);

            foreach(var item in itemCreator.Create(initialCount))
            {
                RegisterNewItem(item);
                list.Add(item);
            }
            _store = new ConcurrentStack<T>(list);
            _totalCount = initialCount;
            _avaliableCunt = initialCount;
            UpdateNextExpandThreshold();
        }
        protected abstract void RegisterNewItem(T item);
        public T Get()
        {

            T item;
            if(_store.TryPop(out item))
            {
                Interlocked.Decrement(ref _avaliableCunt);
                if (_avaliableCunt <= _nextExpandThreshold && _inExpanding == 0)
                    ThreadPool.QueueUserWorkItem(w => TryExpand());

                _itemPreGet?.Invoke(item);

                return item;
            }
            if (_inExpanding == 1)
            {
                var spinWait = new SpinWait();

                while (true)
                {
                    spinWait.SpinOnce();
                    if(_store.TryPop(out item))
                    {
                        Interlocked.Decrement(ref _avaliableCunt);
                        _itemPreGet?.Invoke(item);
                        return item;
                    }
                    if (_inExpanding != 1)
                        return Get();
                }
            }
            else
            {
                TryExpand();
                return Get();
            }
        }
        private bool TryExpand()
        {
            if(Interlocked.CompareExchange(ref _inExpanding, 1, 0) != 0)
            {
                return false;
            }
            Expand();
            _inExpanding = 0;
            return true;
        }
        private void Expand()
        {
            var totalCount = _totalCount;
            foreach(var item in _itemCreator.Create(totalCount))
            {
                _store.Push(item);
                Interlocked.Increment(ref _avaliableCunt);
                RegisterNewItem(item);
            }
            _currentGeneration++;
            _totalCount += totalCount;
            UpdateNextExpandThreshold();
        }
        public virtual bool Shrink()
        {
            var generation = _currentGeneration;
            if (generation == 0)
                return false;

            var shinkThreshold = _totalCount * 3 / 4;
            if (_avaliableCunt <= shinkThreshold)
                return false;

            _currentGeneration = (byte)(generation - 1);
            return true;
        }

        protected abstract bool CanReturn(T item);
        protected abstract bool TryRemove(T item);
        public void Return(T item)
        {
            _itemCleaner?.Invoke(item);

            if (CanReturn(item))
            {
                _store.Push(item);
                Interlocked.Decrement(ref _totalCount);
                return;
            }
            if (TryRemove(item))
                Interlocked.Decrement(ref _totalCount);
        }
        private void UpdateNextExpandThreshold()
        {
            _nextExpandThreshold = _totalCount / 5;
        }
    }
}
