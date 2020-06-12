using UnityEngine;
using System.Collections;
using System.Collections.Generic;
using System.Text;

namespace PerformanceTesting
{
    public static class CSVUtils
    {
        public static string MakeCSVString<T>(List<T> list) 
        {
            StringBuilder sb = new StringBuilder();
            sb.Append(list[0]);
            for (int i = 1; i < list.Count; i++)
            {
                sb.Append(",");
                sb.Append(list[i].ToString());
            }
            //sb.AppendLine();
            return sb.ToString();
        }
    }
}