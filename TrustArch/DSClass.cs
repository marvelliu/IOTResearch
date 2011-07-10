using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace TrustArch
{
    [Serializable]
    public class DSClass
    {
        public double[] m;
        public double[] b;
        public double[] p;
        public int length;

        public DSClass(int n)
        {
            this.m = new double[n];
            this.b = new double[n];
            this.p = new double[n];
            this.length = n;
        }

        public DSClass(double[] newm)
        {
            this.length = newm.Length;
            this.m = new double[this.length];
            this.b = new double[this.length];
            this.p = new double[this.length];
            SetM(newm);
        }

        public void SetM(double[] newm)
        {
            if (newm.Length != this.length)
                throw new Exception("Invalid new m array length.");
            for (int i = 0; i < length; i++)
                this.m[i] = newm[i];
            TestM();
        }


        public void SetM(int i, double newm)
        {
            if (i >= this.length)
                throw new Exception("Invalid new m array length.");
            this.m[i] = newm;
            TestM();
        }

        private int Bit(int num, int offset)
        {
            return (num & (1 << offset))>>offset;
        }

        private bool NotEmpty(int a, int b)
        {
            return (a&b) != 0;
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

        public void TestM()
        {            
            double testresult = 0;
            for (int i = 0; i < this.length; i++)
            {
                testresult += m[i];
            }
            if(testresult>1+0.001)
                throw new Exception("M array bigger than 1."); ;
        }
        public void Cal()
        {
            //首先将不确定度更新
            double total = 0;
            for (int i = 0; i < this.length - 1; i++)
            {
                total += m[i];
            }
            m[this.length - 1] = 1 - total;

            for (int i = 0; i < this.length; i++)
            {
                double resultb = 0;
                double resultp = 0;
                for (int j = 0; j < this.length; j++)
                {
                    //for b[i]
                    if (Contains(i, j))
                        resultb += this.m[j];
                    //for p[i]
                    if (NotEmpty(i, j))
                        resultp += this.m[j];
                }
                this.b[i] = resultb;
                this.p[i] = resultp;                
            }
        }

        public void Output()
        {
            Console.WriteLine("\nOutput:\n\tmn\tb\tp");
            for (int i = 0; i < this.length; i++)
            {
                Console.WriteLine("{0}\t{1:F3}\t{2:F3}\t{3:F3}", i, this.m[i], this.b[i], this.p[i]);
            }
        }
        
        public static DSClass Combine(DSClass a, DSClass b)
        {
            if(a.length != b.length)
            {
                Console.WriteLine("DSClass a and b not equal length.");
                Console.ReadLine();
                return null;
            }
            int n = a.length;
            DSClass ds = new DSClass(n);
            ds.m[0] = 0;
            for (int i = 1; i < n; i++)
            {
                // for ds.m[i]
                double x = 0;
                double y = 0;
                for (int j = 0; j < n; j++)
                {
                    for (int k = 0; k < n; k++)
                    {
                        if ((j & k) == i)
                            x += a.m[j] * b.m[k];
                        if ((j & k) == 0)
                            y += a.m[j] * b.m[k];
                    }                    
                }
                ds.m[i] = x / (1 - y);
            }
            return ds;
        }

        public static DSClass CombineWithWeight(DSClass a, DSClass b, double weighta, double weightb)
        {
            if (a.length != b.length)
            {
                Console.WriteLine("DSClass a and b not equal length.");
                Console.ReadLine();
                return null;
            }
            int n = a.length;
            DSClass ds = new DSClass(n);

            for (int i = 0; i < n; i++)
            {
                ds.m[pow(i)] = a.m[pow(i)] * weighta + b.m[pow(i)] * weightb;
            }
            return ds;
        }

        public static DSClass CombineWithWeight(DSClass[] d, double[] weights)
        {
            int n = d[0].length;
            DSClass ds = new DSClass(n);
            for (int i = 0; i < d.Length;i++ )
            {
                DSClass ds1 = d[i];
                double w = weights[i];
                if (ds1.length != n)
                    throw new Exception("ds length not equal.");
                for (int j = 0; j < ds1.length; j++)
                    ds.m[j] += ds1.m[j] * w;
            }
            return ds;
        }

        public void Normalize()
        {
            IOTGlobal global = (IOTGlobal)IOTGlobal.getInstance();
            //归一化
            double total = 0;
            for (int i = 0; i < this.m.Length; i++)
            {
                if (m[i] < global.SmallValue)
                    m[i] = global.SmallValue;
                total += m[i];
            }
            if (total <= 1)
            {
                m[this.m.Length - 1] += (1 - total);
                return;
            }
            else
            {
                for (int i = 0; i < this.m.Length; i++)
                    m[i] = m[i] / total;
            }
        }


        public static int pow(int i)
        {
            return (int)Math.Pow(2, i);
        }

        public static double AND(double a, double b)
        {
            if (a < 0 || a > 1 || b < 0 || b > 1)
                throw new Exception("a and b not right");
            //return the smaller one
            return (a > b) ? b : a;
        }

        public static double OR(double a, double b)
        {
            if (a < 0 || a > 1 || b < 0 || b > 1)
                throw new Exception("a and b not right");
            //return the smaller one
            return (a > b) ? a : b;
        }

    }
}
