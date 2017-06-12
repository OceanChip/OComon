using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace OceanChip.Common.Components
{
    [AttributeUsage(AttributeTargets.Class)]
    public class ComponentAttribute:Attribute
    {
        public LifeStyle LifeStyle { get; private set; }
        public ComponentAttribute(LifeStyle lifeStyle)
        {
            this.LifeStyle = lifeStyle;
        }
        public ComponentAttribute() : this(LifeStyle.Singleton) { }
    }
    public enum LifeStyle
    {
        /// <summary>
        /// 临时（每次调用重新创建）
        /// </summary>
        Transient,
        /// <summary>
        /// 单件，在运行过程中只创建一次
        /// </summary>
        Singleton,
    }
}
