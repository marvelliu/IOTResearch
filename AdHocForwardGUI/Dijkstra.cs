using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace AdHocBaseApp
{
    public class Dijkstra
    {
        int length;
        public static int noPath = 2000;
        int MaxSize = 1000;
        int[,] G;
        public int[] shortestPath;
        public int[,] shortestPaths;

        public Dijkstra(List<MobileNode> nodes)
        {
            Global global = Global.getInstance();

            length = nodes.Count;
            G = new int[length, length];


            for (int i = 0; i < nodes.Count; i++)
            {
                MobileNode node1 = nodes[i];

                for (int j = 0; j < nodes.Count; j++)
                {
                    MobileNode node2 = nodes[j];
                    double dist = Utility.Distance(node1, node2);
                    //if (dist <= global.nodeMaxDist && i!=j)
                    if (dist <= global.nodeMaxDist)
                        G[i, j] = (int)dist;
                    else
                        G[i, j] = noPath;
                }
            }
        }

        public int GetShortedPath(int start, int end)
        {
            shortestPath = new int[length];
            for (int v = 0; v < length; v++)
                shortestPath[v] = noPath;

            bool[] s = new bool[length]; //表示找到起始结点与当前结点间的最短路径
            int min;  //最小距离临时变量
            int curNode = 0; //临时结点，记录当前正计算结点
            int[] dist = new int[length];
            int[] prev = new int[length];

            //初始结点信息
            for (int v = 0; v < length; v++)
            {
                s[v] = false;
                dist[v] = G[start, v];
                if (dist[v] > MaxSize)
                    prev[v] = noPath;
                else
                    prev[v] = start;
            }
            shortestPath[0] = end;
            dist[start] = 0;
            s[start] = true;
            //主循环
            for (int i = 1; i < length; i++)
            {
                min = MaxSize;
                for (int w = 0; w < length; w++)
                {
                    if (!s[w] && dist[w] < min)
                    {
                        curNode = w;
                        min = dist[w];
                    }
                }

                s[curNode] = true;

                for (int j = 0; j < length; j++)
                    if (!s[j] && min + G[curNode, j] < dist[j])
                    {
                        dist[j] = min + G[curNode, j];
                        prev[j] = curNode;
                    }

            }
            //输出路径结点
            int e = end, step = 0;
            while (e != start)
            {
                step++;
                shortestPath[step] = prev[e];
                e = prev[e];
            }
            for (int i = step; i > step / 2; i--)
            {
                int temp = shortestPath[step - i];
                shortestPath[step - i] = shortestPath[i];
                shortestPath[i] = temp;
            }
            return dist[end];
        }

        //从某一源点出发，找到到所有结点的最短路径
        public int[] GetAllShortedPaths(int start)
        {
            shortestPaths = new int[length, length];
            for (int v = 0; v < length; v++)
                for (int u = 0; u < length; u++)
                    shortestPaths[v, u] = noPath;

            int[] PathID = new int[length];//路径（用编号表示）
            bool[] s = new bool[length]; //表示找到起始结点与当前结点间的最短路径
            int min;  //最小距离临时变量
            int curNode = 0; //临时结点，记录当前正计算结点
            int[] dist = new int[length];
            int[] prev = new int[length];
            //初始结点信息

            for (int v = 0; v < length; v++)
            {
                s[v] = false;
                dist[v] = G[start, v];
                if (dist[v] > MaxSize)
                    prev[v] = noPath;
                else
                    prev[v] = start;
                shortestPaths[v, 0] = v;
            }

            dist[start] = 0;
            s[start] = true;
            //主循环
            for (int i = 1; i < length; i++)
            {
                min = MaxSize;
                for (int w = 0; w < length; w++)
                {
                    if (!s[w] && dist[w] < min)
                    {
                        curNode = w;
                        min = dist[w];
                    }
                }

                s[curNode] = true;

                for (int j = 0; j < length; j++)
                    if (!s[j] && min + G[curNode, j] < dist[j])
                    {
                        dist[j] = min + G[curNode, j];
                        prev[j] = curNode;
                    }

            }
            //输出路径结点
            for (int k = 0; k < length; k++)
            {
                int e = k, step = 0;
                while (e != start && e!=noPath)
                {
                    step++;
                    shortestPaths[k, step] = prev[e];
                    e = prev[e];
                }
                for (int i = step; i > step / 2; i--)
                {
                    int temp = shortestPaths[k, step - i];
                    shortestPaths[k, step - i] = shortestPaths[k, i];
                    shortestPaths[k, i] = temp;
                }
            }
            return dist;

        }
    }
}