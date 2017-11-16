// Copyright 2017 Louis S. Berman.  All rights reserved.

namespace ConnectFetch
{
    public class Job
    {
        public string Href { get; set; }
        public string Name { get; set; }

        public override string ToString() => Name;
    }
}
