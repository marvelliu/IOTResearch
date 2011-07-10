using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace TrustArch
{
    class HashDSClass
    {
        private Dictionary<int, double> m;
        public int length;

        public HashDSClass(int length)
        {
            this.length = length;
            this.m = new Dictionary<int, double>();
        }

        public HashDSClass(int length, Dictionary<int, double> ms)
        {
            this.length = length;
            this.m = ms;
        }

        public void SetM(int i, double newm)
        {
            if (i >= this.length)
                throw new Exception("Invalid new m array length.");
            if (this.m.ContainsKey(i))
                this.m[i] = newm;
            else
                this.m.Add(i, newm);
        }
        public double GetM(int i)
        {
            IOTGlobal global = (IOTGlobal)IOTGlobal.getInstance();
            if (i >= this.length)
                throw new Exception("Invalid new m array length.");
            if (this.m.ContainsKey(i))
                return this.m[i];
            else
                return global.SmallValue;
        }

        private int Bit(int num, int offset)
        {
            return (num & (1 << offset)) >> offset;
        }

        private bool NotEmpty(int a, int b)
        {
            return (a & b) != 0;
        }

        public bool Contains(int a, int b)
        {
            for (int i = 0; i < sizeof(int); i++)
            {
                if (Bit(a, i) == 0 && Bit(b, i) == 1)
                    return false;

            }
            return true;
        }

        public double[] Cal(int x)
        {
            double[] result = new double[3];
            double resultb = 0;
            double resultp = 0;
            foreach (KeyValuePair<int, double> k in this.m)
            {
                int i = k.Key;
                double v = k.Value;
                //for b[i]
                if (Contains(x, i))
                    resultb += v;
                //for p[i]
                if (NotEmpty(x, i))
                    resultp += v;
            }
            result[0] = resultb;
            result[1] = resultb;
            result[2] = resultp;
            return result;
        }

        public void Output()
        {
            Console.WriteLine("\nOutput:");
            for (int i = 0; i < this.length; i++)
            {
                double[] r = Cal(i);
                Console.WriteLine("{0}\t{1:F3}\t{2:F3}\t{3:F3}", i, r[0], r[1], r[2]);
            }
        }

        //归一化的函数
        public void Normalize()
        {
            double totalv = 0;
            foreach (KeyValuePair<int, double> k in this.m)
            {
                int i = k.Key;
                double v = k.Value;
                totalv += v;
            }
            int[] l =  this.m.Keys.ToArray();

            for (int i =0;i<l.Length;i++)
            {
                int key = l[i];
                this.m[key] = this.m[key] / totalv;
            }
        }

    }
}
