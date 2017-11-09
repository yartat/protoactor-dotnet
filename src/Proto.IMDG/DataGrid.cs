using System;
using System.Collections.Generic;
using System.Text;

namespace Proto.IMDG
{
    public static class DataGrid
    {
        public static ListProxy<T> GetList<T>(string name) => new ListProxy<T>(name);
    }
}
