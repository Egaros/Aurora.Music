﻿using System;
using System.Collections.Generic;
using System.Linq;

namespace LrcParser
{
    public class Lyric
    {

        public Lyric(IOrderedEnumerable<Slice> orderedEnumerable, List<KeyValuePair<string, string>> enumerable)
        {
            this.Slices = orderedEnumerable;
            this.AddtionalInfo = enumerable;
            foreach (var item in AddtionalInfo)
            {
                if (item.Key.Equals("offset", StringComparison.CurrentCultureIgnoreCase))
                {
                    if (int.TryParse(item.Value, out int i))
                    {
                        Offset = TimeSpan.FromMilliseconds(i);
                        AddtionalInfo.Remove(item);
                        break;
                    }
                }
            }
        }

        public Lyric(string s, TimeSpan duration)
        {
            var sl = s.Split('\n');

            Slices = sl.Select(x => new Slice
            {
                Offset = TimeSpan.FromMilliseconds(duration.TotalMilliseconds * (Array.IndexOf(sl, x) / (double)sl.Length)),
                Contet = x
            }).OrderBy(x => 1);
            AddtionalInfo = null;
        }

        public IOrderedEnumerable<Slice> Slices { get; set; }
        public List<KeyValuePair<string, string>> AddtionalInfo { get; set; }

        public TimeSpan Offset { get; set; }
    }

    public class Slice
    {
        public TimeSpan Offset { get; set; }
        public string Contet { get; set; }
    }
}