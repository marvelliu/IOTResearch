using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace Test
{
    class Program
    {
        static void Main(string[] args)
        {
            int[] a = new int[] { 1, 2, 3, 4, 5, 6, 7, 8 ,9,10,11,12};
            int m = 0; // 待取出组合的个数   
            Combination c = new Combination();
            List<int[]> list = c.combination(a, m);
            c.print(list);
            Console.WriteLine("一共" + list.Count + "组!");

        }

    }
}
