#if PORTABLE

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Mariasek.Engine.New
{
    public class DescriptionAttribute : Attribute
    {
        public string Description { get; set; }

        public DescriptionAttribute()
            : this(string.Empty)
        {
        }

        public DescriptionAttribute(string description)
        {
            Description = description;
        }
    }
}

#endif