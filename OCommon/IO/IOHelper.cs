using OceanChip.Common.Extensions;
using OceanChip.Common.Logging;
using OceanChip.Common.Utilities;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace OceanChip.Common.IO
{
    public class IOHelper
    {
        private readonly ILogger _logger;

        public IOHelper(ILoggerFactory loggerFactory)
        {
            this._logger = loggerFactory.Create(GetType().FullName);
        }

        public void TryIOAction(string actionName,Func<string> getContext,Action action,int maxRetryTimes,bool continueRetryWhenRetryFailed=false,int retryInterval=1000)
        {
            Check.NotNull(actionName, nameof(actionName));
            Check.NotNull(getContext, nameof(getContext));
            Check.NotNull(action, nameof(action));
            TryIOActionRecursivelyInternal(actionName, getContext, (x, y, z) => action(), 0, maxRetryTimes, continueRetryWhenRetryFailed, retryInterval);
        }


        public T TryIOFunc<T>(string funcName,Func<string> getContent,Func<T> func,int maxRetryTimes, bool continueRetryWhenRetryFailed = false, int retryInterval = 1000)
        {
            Check.NotNull(funcName, nameof(funcName));
            Check.NotNull(getContent, nameof(getContent));
            Check.NotNull(func, nameof(func));
            return TryIOFuncRecursivelyInternal(funcName, getContent, (x, y, DivideByZeroException) => func(), 0, maxRetryTimes, continueRetryWhenRetryFailed, retryInterval);
        }
        public void TryAsyncActionRecursively<TAsyncResult>(
            string asyncActionName,
            Func<Task<TAsyncResult>> asyncAction,
            Action<int> mainAction,
            Action<TAsyncResult> successAction,
            Func<string> getContextFunc,
            Action<string> failedAction,
            int retryTimes,
            bool retryWhenFailed=false,
            int maxRetryTimes=3,
            int retryInterval=1000)where TAsyncResult:AsyncTaskResult
        {
            try
            {
                asyncAction().ContinueWith(TaskContinueAction,new TaskExecutionContext<TAsyncResult>{
                    AsyncActionName=asyncActionName,
                    MainAction=mainAction,
                    SuccessAction=successAction,
                    GetContextInfoFunc=getContextFunc,
                    FailedAction=failedAction,
                    RetryTimes=retryTimes,
                    RetryWhenFailed=retryWhenFailed,
                    MaxRetryTimes=maxRetryTimes,
                    RetryInterval=retryInterval
                });
            }catch(IOException ex)
            {
                _logger.Error($"执行异步任务'{asyncActionName}'引发IOException异常，contextInfo:{GetContextInfo(getContextFunc)},current retryTimes:{retryTimes},尝试重新执行", ex);
                ExecuteRetryAction(asyncActionName, getContextFunc, mainAction, retryTimes, maxRetryTimes, retryInterval);
            }catch(Exception ex)
            {
                _logger.Error($"执行异步任务'{asyncActionName}'出现未知异常，contextInfo:{GetContextInfo(getContextFunc)},current retryTimes:{retryTimes}", ex);
                if (retryWhenFailed)
                {
                    ExecuteRetryAction(asyncActionName, getContextFunc, mainAction, retryTimes, maxRetryTimes, retryInterval);
                }
                else
                {
                    ExecuteFailedAction(asyncActionName, getContextFunc, failedAction, ex.Message);
                }
            }
        }
        public void TryIOAction(Action action,string actionName)
        {
            Check.NotNull(action, nameof(action));
            Check.NotNull(actionName, nameof(actionName));

            try
            {
                action();
            }
            catch (IOException)
            {
                throw;
            }catch(Exception ex)
            {
                throw new IOException($"{actionName}执行失败", ex);
            }
        }
        public Task TryIOActionAsync(Func<Task> action, string actionName)
        {
            Check.NotNull(action, nameof(action));
            Check.NotNull(actionName, nameof(actionName));

            try
            {
               return action();
            }
            catch (IOException)
            {
                throw;
            }
            catch (Exception ex)
            {
                throw new IOException($"{actionName}执行失败", ex);
            }
        }
        public T TryIOFunc<T>(Func<T> func, string funName)
        {
            Check.NotNull(func, nameof(func));
            Check.NotNull(funName, nameof(funName));

            try
            {
                return func();
            }
            catch (IOException)
            {
                throw;
            }
            catch (Exception ex)
            {
                throw new IOException($"{funName}执行失败", ex);
            }
        }
        public Task<T> TryIOFunc<T>(Func<Task<T>> func, string funName)
        {
            Check.NotNull(func, nameof(func));
            Check.NotNull(funName, nameof(funName));

            try
            {
                return func();
            }
            catch (IOException)
            {
                throw;
            }
            catch (Exception ex)
            {
                throw new IOException($"{funName}执行失败", ex);
            }
        }
        private void ExecuteRetryAction(string asyncActionName, Func<string> getContextFunc, Action<int> mainAction, int currentRetryTimes, int maxRetryTimes, int retryInterval)
        {
            try
            {
                if(currentRetryTimes >= maxRetryTimes){
                    Task.Factory.StartDelayedTask(retryInterval, () => mainAction(currentRetryTimes + 1));
                }else{
                    mainAction(currentRetryTimes + 1);
                }
            }catch(Exception ex)
            {
                _logger.Error($"执行retryAction失败，asyncActionName:{asyncActionName},contextInfo:{GetContextInfo(getContextFunc)}", ex);
            }
        }

        private void ExecuteFailedAction(string asyncActionName, Func<string> getContextFunc, Action<string> failedAction, string message)
        {
            try
            {
                failedAction?.Invoke(message);
            }
            catch (Exception ex)
            {
                _logger.Error("执行failedAction函数异常", ex);
            }
        }

        private string GetContextInfo(Func<string> func)
        {
            try
            {
                return func();
            }catch(Exception ex)
            {
                _logger.Error("执行getContextFunc函数异常", ex);
                return null;
            }
        }

        private void ProcessTaskException(
            string asyncActionName,
            Func<string> getContextFunc,
            Action<int> mainAction,
            Action<string> failedAction,
            Exception exception,
            int currentRetryTimes,
            int maxRetryTimes,
            int retryInterval,
            bool retryWhenFailed)
        {
            if (exception is IOException)
            {
                _logger.Error($"异步任务 '{asyncActionName}' 引发 IOException异常, contextInfo:{GetContextInfo(getContextFunc)}, " +
                    $"current retryTimes:{currentRetryTimes}, 尝试重新执行.");
                ExecuteRetryAction(asyncActionName, getContextFunc, mainAction, currentRetryTimes, maxRetryTimes, retryInterval);
            }
            else
            {
                _logger.Error($"异步任务 '{asyncActionName}' 未知异常, contextInfo:{GetContextInfo(getContextFunc)}, " +
                    $"current retryTimes:{currentRetryTimes},.");
                if (retryWhenFailed)
                {
                    ExecuteRetryAction(asyncActionName, getContextFunc, mainAction, currentRetryTimes, maxRetryTimes, retryInterval);
                }
                else
                {
                    ExecuteFailedAction(asyncActionName, getContextFunc, failedAction, exception.Message);
                }
            }
        }
        private T TryIOFuncRecursivelyInternal<T>(string funcName, Func<string> getContext, Func<string, Func<string>, long, T> func, int retryTimes, int maxRetryTimes, bool continueRetryWhenRetryFailed, int retryInterval)
        {
            try
            {
                return func(funcName, getContext, retryTimes);
            }
            catch (IOException ex)
            {
                var errorMessage = $"异步任务 '{funcName}' 引发 IOException异常, contextInfo:{GetContextInfo(getContext)}, " +
                    $"current retryTimes:{retryTimes}, maxRetryTimes:{maxRetryTimes}";
                _logger.Error(errorMessage, ex);
                if (retryTimes >= maxRetryTimes)
                {
                    if (!continueRetryWhenRetryFailed)
                        throw;
                    else
                        Thread.Sleep(retryInterval);
                }
                retryTimes++;
                return TryIOFuncRecursivelyInternal(funcName, getContext, func, retryTimes, maxRetryTimes, continueRetryWhenRetryFailed, retryInterval);
            }catch(Exception ex)
            {
                _logger.Error($"异步任务 '{funcName}' 发生未知异常, contextInfo:{GetContextInfo(getContext)}, " +
                    $"current retryTimes:{retryTimes}, maxRetryTimes:{maxRetryTimes}",ex);
                throw;
            }
        }

        private void TryIOActionRecursivelyInternal(string actionName, Func<string> getContext, Action<string, Func<string>, int> action, int retryTimes, int maxRetryTimes, bool continueRetryWhenRetryFailed=false, int retryInterval=1000)
        {
            try
            {
                action(actionName, getContext, retryTimes);
            }catch(IOException ex)
            {
                var errorMessage=$"异步任务 '{actionName}' 引发 IOException异常, contextInfo:{GetContextInfo(getContext)}, " +
                    $"current retryTimes:{retryTimes}, maxRetryTimes:{maxRetryTimes}";
                _logger.Error(errorMessage, ex);
                if (retryTimes >= maxRetryTimes)
                {
                    if (!continueRetryWhenRetryFailed)
                        throw;
                    else
                        Thread.Sleep(retryInterval);
                }
                retryTimes++;
                TryIOActionRecursivelyInternal(actionName, getContext, action, retryTimes, maxRetryTimes, continueRetryWhenRetryFailed, retryInterval);
            }
            catch (Exception ex)
            {
                _logger.Error($"异步任务 '{actionName}' 发生未知异常, contextInfo:{GetContextInfo(getContext)}, " +
                    $"current retryTimes:{retryTimes}, maxRetryTimes:{maxRetryTimes}", ex);
                throw;
            }
        }
        private void TaskContinueAction<TAsyncResult>(Task<TAsyncResult> task, object obj) where TAsyncResult : AsyncTaskResult
        {
            var context = obj as TaskExecutionContext<TAsyncResult>;
            try
            {
                if (task.Exception != null)
                {
                    ProcessTaskException(
                        context.AsyncActionName,
                        context.GetContextInfoFunc,
                        context.MainAction,
                        context.FailedAction,
                        task.Exception,
                        context.RetryTimes,
                        context.MaxRetryTimes,
                        context.RetryInterval,
                        context.RetryWhenFailed);
                    return;
                }
                if (task.IsCanceled)
                {
                    _logger.Error($"异步任务 '{context.AsyncActionName}' 被取消, contextInfo:{ GetContextInfo(context.GetContextInfoFunc)}, current retryTimes:{context.RetryTimes}.");
                    ExecuteFailedAction(
                        context.AsyncActionName,
                        context.GetContextInfoFunc,
                        context.FailedAction,
                        $"异步任务 '{context.AsyncActionName}' 被取消");
                    return;
                }
                var result = task.Result;
                if (result == null)
                {
                    _logger.Error($"异步任务 '{context.AsyncActionName}' 结果为NULL, contextInfo:{ GetContextInfo(context.GetContextInfoFunc)}, current retryTimes:{context.RetryTimes}.");
                    if (context.RetryWhenFailed)
                    {
                        ExecuteRetryAction(
                            context.AsyncActionName,
                            context.GetContextInfoFunc,
                            context.MainAction,
                            context.RetryTimes,
                            context.MaxRetryTimes,
                            context.RetryInterval);
                    }
                    else
                    {
                        ExecuteFailedAction(
                            context.AsyncActionName,
                            context.GetContextInfoFunc,
                            context.FailedAction,
                            $"异步任务 '{context.AsyncActionName}' 结果为NULL");
                    }
                    return;
                }
                if (result.Status == AsyncTaskStatus.Success)
                {
                    if (context.SuccessAction != null)
                    {
                        context.SuccessAction(result);
                    }
                }
                else if (result.Status == AsyncTaskStatus.IOException)
                {
                    _logger.Error($"异步任务 '{context.AsyncActionName}' 结果状态为 IOException, contextInfo:{GetContextInfo(context.GetContextInfoFunc)}, " +
                        $"current retryTimes:{context.RetryTimes}, errorMsg:{result.ErrorMessage}, 尝试重新执行.");
                    ExecuteRetryAction(
                        context.AsyncActionName,
                        context.GetContextInfoFunc,
                        context.MainAction,
                        context.RetryTimes,
                        context.MaxRetryTimes,
                        context.RetryInterval);
                }
                else if (result.Status == AsyncTaskStatus.Failed)
                {
                    _logger.ErrorFormat($"异步任务 '{context.AsyncActionName}' 执行失败, contextInfo:{GetContextInfo(context.GetContextInfoFunc)}, " +
                        $"current retryTimes:{context.RetryTimes}, errorMsg:{result.ErrorMessage}");
                    if (context.RetryWhenFailed)
                    {
                        ExecuteRetryAction(
                            context.AsyncActionName,
                            context.GetContextInfoFunc,
                            context.MainAction,
                            context.RetryTimes,
                            context.MaxRetryTimes,
                            context.RetryInterval);
                    }
                    else
                    {
                        ExecuteFailedAction(
                            context.AsyncActionName,
                            context.GetContextInfoFunc,
                            context.FailedAction,
                            result.ErrorMessage);
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.Error($"执行taskContinueAction失败, asyncActionName:{context.AsyncActionName}, contextInfo:{GetContextInfo(context.GetContextInfoFunc)}", ex);
            }
        }
        class TaskExecutionContext<TAsyncResult>
        {
            public string AsyncActionName;
            public Action<int> MainAction;
            public Action<TAsyncResult> SuccessAction;
            public Func<string> GetContextInfoFunc;
            public Action<string> FailedAction;
            public int RetryTimes;
            public bool RetryWhenFailed;
            public int MaxRetryTimes;
            public int RetryInterval;
        }
    }
}
