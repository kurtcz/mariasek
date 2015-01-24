using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace Mariasek.Engine
{
    public static class LinqExtensions
    {
        private static readonly Random rand = new Random();

        public static TSource RandomOne<TSource>(this ICollection<TSource> source)
        {
            return source.ElementAt(rand.Next(source.Count()));
        }

        public static TSource RandomOneOrDefault<TSource>(this ICollection<TSource> source)
        {
            if (source.Any())
            {
                return RandomOne(source);
            }

            return default(TSource);
        }
    }
}
