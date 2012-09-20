using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Reflection;
using System.Runtime.Serialization;

namespace AdHocBaseApp
{
    public static class Utility
    {
        public const int MAX_VAL = 10000;

        //public static Random ran = new Random((int)DateTime.Now.Ticks); 
        public static Random ran = new Random(1223);


        //public static Random ran;
        
        public static int Rand(int max)
        {
            return ran.Next(max);
        }

        public static double U_Rand(double max)      //    均匀分布 
        {
            return ran.NextDouble() * max;
        }

        public static double U_Rand(double a, double b)      //    均匀分布  
        {
            double x = ran.Next(MAX_VAL);
            return a + (b - a) * x / (MAX_VAL - 1);
        }

        //产生的是自然数，如果产生float或double需要*N/N
        public static double P_Rand(double Lamda)         //    泊松分布    
        {
            double x = 0, b = 1, c = Math.Exp(-Lamda), u;
            do
            {
                u = U_Rand(0, 1);
                b *= u;
                if (b >= c)
                    x++;
            } while (b >= c);
            return x;
        }
        public static double P_Rand1( double Lamda)         //    泊松分布    
        {
            double u;
            int p = 1;
            int n = 2 * (int)Lamda;
            double[] f = new double[n];
            u = U_Rand(0, 1);  
            for (int i = 0; i < n; i++)
            {
                if(i!=0)
                    p*= i;
                f[i] = Math.Exp(-Lamda) * Math.Pow(Lamda, i)/p ;
                if(i!=0)
                    f[i] += f[i - 1];
                if (i == 0 && f[i] > u)
                    return i;
                if (i > 0 && f[i - 1] < u && f[i] > u)
                    return i;
            }  

            return -1;
        }
        public static double Distance(double x1, double y1, double x2, double y2)
        {
            return Math.Sqrt((x2 - x1) * (x2 - x1) + (y2 - y1) * (y2 - y1));
        }

        public static double IncludedAngle(MobileNode n1, MobileNode n2)
        {
            double deltax = n2.X - n1.X;
            //double deltay = n1.Y - n2.Y;
            double deltay = n1.Y - n2.Y; //因为画图是，y是向下增长的，所以需要变换
            double angle = Math.Atan(deltay / deltax);
            if (deltay < 0)
                angle += Math.PI;
            //On1n2
            return angle;
        }


        public static double Distance(MobileNode n1, MobileNode n2)
        {
            return Math.Sqrt((n2.X - n1.X) * (n2.X - n1.X) + (n2.Y - n1.Y) * (n2.Y - n1.Y));
        }

        public static bool DoubleEqual(double a, double b)
        {
            if (Math.Abs(a - b) < 0.00001f)
                return true;
            else
                return false;
        }

        public static int Power(int n)
        {
            return (int)Math.Pow(2, n);
        }

        public static double Max(double[] list)
        {
            double r = list[0];
            for (int i = 1; i < list.Length; i++)
            {
                if (list[i] > r)
                    r = list[i];
            }
            return r;
        }


        public static double Average(double[] list)
        {
            double sum = 0;
            for (int i = 1; i < list.Length; i++)
            {
                sum += list[i];
            }
            return sum/list.Length;
        }

        public static string DumpHashIntSet(HashSet<int> set)
        {
            if (set == null)
                return null;
            StringBuilder sb = new StringBuilder();
            sb.AppendFormat("count:{0}\t--", set.Count);
            foreach (int o in set)
                sb.AppendFormat("{0}\t", o);
            return sb.ToString();
        }

        public static string DumpArrayIntSet(int[] l)
        {
            if (l == null)
                return null;
            StringBuilder sb = new StringBuilder();
            foreach (int o in l)
                sb.AppendFormat("{0}\t", o);
            return sb.ToString();
        }

        public static string DumpListIntSet(List<int> l)
        {
            if (l == null)
                return null;
            StringBuilder sb = new StringBuilder();
            foreach (int o in l)
                sb.AppendFormat("{0}\t", o);
            return sb.ToString();
        }



        public static int[] SortDictionary(Dictionary<int, int> d)
        {
            Dictionary<int, HashSet<int>> hashedKeys = new Dictionary<int,HashSet<int>>();
            foreach (KeyValuePair<int, int> pair in d)
            {
                int key = pair.Key;
                int val = pair.Value;
                if (!hashedKeys.ContainsKey(val))
                    hashedKeys.Add(val, new HashSet<int>());
                hashedKeys[val].Add(key);
            }
            int[] keys = hashedKeys.Keys.ToArray();
            System.Array.Sort<int>(keys); //顺序排列
            int[] result = new int[d.Count];
            int i = 0;
            for(int n=keys.Length-1;n>=0;n--)
            {
                foreach (int k in hashedKeys[keys[n]])
                {
                    result[i] = k;
                    i++;
                }
            }
            return result;

        }

        public static void AddTo(List<object> from, List<object> to)
        {
            foreach (object f in from)
                to.Add(f);
        }

        public static void AddHashSet<T>(HashSet<T> to, List<T> from)
        {
            foreach (T o in from)
            {
                if (!to.Contains(o))
                    to.Add(o);
            }
        }

        public static void AddHashSet<T>(HashSet<T> to, HashSet<T> from)
        {
            foreach (T o in from)
                to.Add(o);
        }

        public static void CopyHashSet<T>(HashSet<T> to, HashSet<T> from)
        {
            to.Clear();
            foreach (T o in from)
                to.Add(o);
        }

        public static bool IsSameHashSet<T>(HashSet<T> s1, HashSet<T> s2)
        {
            if (s1.Count != s2.Count)
                return false;
            foreach (T o in s1)
            {
                if (!s2.Contains(o))
                    return false;
            }
            return true;
        }

        /*
        public static HashSet<T> CloneHashSet<T>(this HashSet<T> original)
        {
            var clone = (HashSet<T>)FormatterServices.GetUninitializedObject(typeof(HashSet<T>));
            CopyValue(Fields<T>.comparer, original, clone);

            if (original.Count == 0)
            {
                Fields<T>.freeList.SetValue(clone, -1);
            }
            else
            {
                Fields<T>.count.SetValue(clone, original.Count);
                CloneValue(Fields<T>.buckets, original, clone);
                CloneValue(Fields<T>.slots, original, clone);
                CopyValue(Fields<T>.freeList, original, clone);
                CopyValue(Fields<T>.lastIndex, original, clone);
                CopyValue(Fields<T>.version, original, clone);
            }

            return clone;
        }

        static void CopyValue<T>(FieldInfo field, HashSet<T> source, HashSet<T> target)
        {
            field.SetValue(target, field.GetValue(source));
        }

        static void CloneValue<T>(FieldInfo field, HashSet<T> source, HashSet<T> target)
        {
            field.SetValue(target, ((Array)field.GetValue(source)).Clone());
        }

        static class Fields<T>
        {
            public static readonly FieldInfo freeList = GetFieldInfo("m_freeList");
            public static readonly FieldInfo buckets = GetFieldInfo("m_buckets");
            public static readonly FieldInfo slots = GetFieldInfo("m_slots");
            public static readonly FieldInfo count = GetFieldInfo("m_count");
            public static readonly FieldInfo lastIndex = GetFieldInfo("m_lastIndex");
            public static readonly FieldInfo version = GetFieldInfo("m_version");
            public static readonly FieldInfo comparer = GetFieldInfo("m_comparer");

            static FieldInfo GetFieldInfo(string name)
            {
                return typeof(HashSet<T>).GetField(name, BindingFlags.Instance | BindingFlags.NonPublic);
            }
        }*/
        //结束复制hashset



    }

}
