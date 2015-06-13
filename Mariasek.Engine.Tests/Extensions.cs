using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Reflection;

namespace Mariasek.Engine.Tests
{
    internal static class Extensions
    {
        internal static Dictionary<string, object> ToPropertyDictionary(this object obj)
        {
            var dict = new Dictionary<string, object>();

            foreach (var prop in obj.GetType().GetProperties(BindingFlags.Public | BindingFlags.NonPublic | 
                BindingFlags.Instance | BindingFlags.Static))
            {
                dict.Add(prop.Name, prop.GetValue(obj));
            }

            foreach (var field in obj.GetType().GetFields(BindingFlags.Public | BindingFlags.NonPublic |
                BindingFlags.Instance | BindingFlags.Static))
            {
                dict.Add(field.Name, field.GetValue(obj));
            }

            return dict;
        }
    }
}
