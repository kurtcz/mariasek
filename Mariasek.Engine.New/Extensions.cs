using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Text;
using System.Reflection;
using System.Threading.Tasks;

namespace Mariasek.Engine.New
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
                var typeInfo = value.GetType().GetTypeInfo();
                var attributes = typeInfo.GetCustomAttributes(false)
                                      .Select(j => j as DescriptionAttribute).ToArray();
#else
                var fieldInfo = value.GetType().GetField(entries[i].Trim());
                var attributes = (DescriptionAttribute[])fieldInfo.GetCustomAttributes(typeof(DescriptionAttribute), false);
#endif
                description[i] = (attributes.Length > 0) ? attributes[0].Description : entries[i].Trim();
            }
            return String.Join(", ", description);
        }
            
        public static string ToDescription(this Mariasek.Engine.New.Barva b)
        {
            switch (b)
            {
                case Barva.Cerveny:
                    return "červený";
                case Barva.Zeleny:
                    return "zelený";
                case Barva.Kule:
                    return "kule";
                case Barva.Zaludy:
                    return "žaludy";
            }
            return string.Empty;
        }
    }
}
