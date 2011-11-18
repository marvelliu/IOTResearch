using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace Test
{
    class Combination
    {
        public List<int[]> combination(int[] a, int m)
        {
            Combination c = new Combination();
            List<int[]> list = new List<int[]>();

            int n = a.Length;
            if (m == 0)
            {
                list.Add(new int[] { });
                return list;
            }
            else if (m == n)
            {
                int[] temp = new int[a.Length];
                a.CopyTo(temp, 0);
                list.Add(a);
                return list;
            }

            bool end = false; // 是否是最后一种组合的标记   
            // 生成辅助数组。首先初始化，将数组前n个元素置1，表示第一个组合为前n个数。   
            int[] tempNum = new int[n];
            for (int i = 0; i < n; i++)
            {
                if (i < m)
                {
                    tempNum[i] = 1;

                }
                else
                {
                    tempNum[i] = 0;
                }
            }
            printVir(tempNum);// 打印首个辅助数组   
            list.Add(c.createResult(a, tempNum, m));// 打印第一种默认组合   
            int k = 0;//标记位   
            while (!end)
            {
                bool findFirst = false;
                bool swap = false;
                // 然后从左到右扫描数组元素值的"10"组合，找到第一个"10"组合后将其变为"01"   
                for (int i = 0; i < n; i++)
                {
                    int l = 0;
                    if (!findFirst && tempNum[i] == 1)
                    {
                        k = i;
                        findFirst = true;
                    }
                    if (tempNum[i] == 1 && tempNum[i + 1] == 0)
                    {
                        tempNum[i] = 0;
                        tempNum[i + 1] = 1;
                        swap = true;
                        for (l = 0; l < i - k; l++)
                        { // 同时将其左边的所有"1"全部移动到数组的最左端。   
                            tempNum[l] = tempNum[k + l];
                        }
                        for (l = i - k; l < i; l++)
                        {
                            tempNum[l] = 0;
                        }
                        if (k == i && i + 1 == n - m)
                        {//假如第一个"1"刚刚移动到第n-m+1个位置,则终止整个寻找   
                            end = true;
                        }
                    }
                    if (swap)
                    {
                        break;
                    }
                }
                printVir(tempNum);// 打印辅助数组   
                list.Add(c.createResult(a, tempNum, m));// 添加下一种默认组合   
            }
            return list;
        }

        // 根据辅助数组和原始数组生成结果数组   
        public int[] createResult(int[] a, int[] temp, int m) {
        int[] result = new int[m];
        int j = 0;
        for (int i = 0; i < a.Length; i++) {
            if (temp[i] == 1) {
                result[j] = a[i];
                Console.WriteLine("result[" + j + "]:" + result[j]);
                j++;
            }
        }
        return result;
    }

        // 打印整组数组   
        public void print(List<int[]> list) {
        Console.WriteLine("具体组合结果为:");
        for (int i = 0; i < list.Count; i++) {
            int[] temp = (int[]) list[i];
            for (int j = 0; j < temp.Length; j++) {
                Console.Write(temp[j] + " ");
            }
            Console.WriteLine();
        }
    }

        // 打印辅助数组的方法   
        public void printVir(int[] a) {
        Console.WriteLine("生成的辅助数组为：");
        for (int i = 0; i < a.Length; i++) {
            Console.Write(a[i]);
        }
        Console.WriteLine();
    }

    }
}
