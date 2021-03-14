using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Text;
using System.Reflection;
using System.Threading.Tasks;

namespace Mariasek.Engine
{
    public static class Extensions
    {
        /// <summary>
        /// Fetches a next value from an enum.
        /// </summary>
        public static T Next<T>(this T src) where T : struct
        {
            //It's possible that this code does not work on Android/iOS targets
#if !PORTABLE
            if (!typeof(T).IsEnum) throw new ArgumentException(String.Format("Argument {0} is not an Enum", typeof(T).FullName));
#else
            if (!typeof(T).GetTypeInfo().IsEnum) throw new ArgumentException(String.Format("Argument {0} is not an Enum", typeof(T).FullName));
#endif

            T[] Arr = (T[])Enum.GetValues(src.GetType());
            int j = Array.IndexOf<T>(Arr, src) + 1;
            return (Arr.Length == j) ? Arr[0] : Arr[j];
        }
        private const char ENUM_SEPERATOR_CHARACTER = ',';

        public static string Description(this Enum value)
        {
            // Check for Enum that is marked with FlagAttribute
            var entries = value.ToString().Split(ENUM_SEPERATOR_CHARACTER);
            var description = new string[entries.Length];
            for (var i = 0; i < entries.Length; i++)
            {
#if PORTABLE
                var fieldInfo = value.GetType().GetRuntimeField(entries[i].Trim());
                var attributes = fieldInfo != null ? fieldInfo.GetCustomAttributes<DescriptionAttribute>(false).ToArray() : null;
#else
                var fieldInfo = value.GetType().GetField(entries[i].Trim());
                var attributes = (DescriptionAttribute[])fieldInfo.GetCustomAttributes(typeof(DescriptionAttribute), false);
#endif
                description[i] = (attributes != null && attributes.Length > 0) ? attributes[0].Description : entries[i].Trim();
			}
            return String.Join(", ", description);
        }

        public static string ToDescription(this Hra gt, Barva? trump = null, bool showGiveUp = false)
        {
            if (gt == 0)
            {
                return showGiveUp ? "Vzdát" : string.Empty;
            }
            if ((gt & Hra.Hra) != 0)
            {
                if ((gt & Hra.Sedma) != 0)
                {
                    return string.Format("Sedma {0}", trump != null ? trump.Value.Description() : "");
                }
                else
                {
                    return string.Format("Hra {0}", trump != null ? trump.Value.Description() : "");
                }
            }
            else if ((gt & Hra.Kilo) != 0)
            {
                if ((gt & Hra.Sedma) != 0)
                {
                    return string.Format("Stosedm {0}", trump != null ? trump.Value.Description() : "");
                }
                else
                {
                    return string.Format("Kilo {0}", trump != null ? trump.Value.Description() : "");
                }
            }
            else if (gt == Hra.Betl)
            {
                return "Betl";
            }
            else if (gt == Hra.Durch)
            {
                return "Durch";
            }

            return string.Empty;
        }

        public static bool ContainsCancellationException(this Exception ex)
        {
            for (var current = ex; current != null; current = current.InnerException)
            {
                var ae = current as AggregateException;

                if (ae != null)
                {
                    foreach(var inner in ae.Flatten().InnerExceptions)
                    {
                        if (inner is OperationCanceledException)
                        {
                            return true;
                        }
                    }
                }
                if (current is OperationCanceledException)
                {
                    return true;
                }
            }

            return false;
		}
    
        public static string StringBeforeToken(this string text, string token)
        {
            var index = text.IndexOf(token);

            if (index >= 0)
            {
                return text.Substring(0, index);
            }

            return text;
        }
    }
}
