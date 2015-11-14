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
        /// <summary>
        /// Gets all of object's public and non-public fields and properties as a dictionary
        /// </summary>
        /// <param name="obj"></param>
        /// <returns></returns>
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

        /// <summary>
        /// Invokes object's method even if it is not public.
        /// </summary>
        internal static T InvokeMethod<T>(this object obj, string methodName, params object[] methodParams)
        {
            MethodInfo dynMethod = obj.GetType().GetMethod(methodName,
                BindingFlags.NonPublic | BindingFlags.Instance);
            T result = (T)dynMethod.Invoke(obj, methodParams);

            return result;
        }

        internal static void InvokeMethod(this object obj, string methodName, params object[] methodParams)
        {
            MethodInfo dynMethod = obj.GetType().GetMethod(methodName,
                BindingFlags.NonPublic | BindingFlags.Instance);
            dynMethod.Invoke(obj, methodParams);
        }
    }
}
