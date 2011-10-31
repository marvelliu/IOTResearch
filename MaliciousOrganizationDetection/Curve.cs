using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace MaliciousOrganizationDetection
{
    //最小二乘法的曲线
    public class LinearRegression
    {
        public double a;
        public double b;

        double[,] V;            // Least squares and var/covar matrix
        public double[] C;      // Coefficients
        public double[] SEC;    // Std Error of coefficients
        double RYSQ;            // Multiple correlation coefficient
        double SDV;             // Standard deviation of errors
        double FReg;            // Fisher F statistic for regression
        double[] Ycalc;         // Calculated values of Y
        double[] DY;            // Residual values of Y

        MODGlobal global = (MODGlobal)MODGlobal.getInstance();

        public void BuildLSMCurve(double[] arcs, List<double> history, int order, bool logY)
        {

            int N = order;
            double[] y = new double[history.Count];
            double[,] x = new double[N + 1, history.Count];
            double[] w = new double[history.Count];


            for (int i = 0; i < history.Count; i++)
            {
                    x[0, i] = 1;
                    double xx = arcs[i];
                    double term = xx;
                    for (int j = 1; j <= N; j++)
                    {
                        x[j, i] = term;
                        term *= xx;
                    }
                    if (logY)
                    {
                        y[i] = Math.Log(history[i]);
                        w[i] = 1;
                    }
                    else
                    {
                        y[i] = history[i];
                        w[i] = 1;
                    }
            }

            this.Regress(y, x, w);

            return;
        }

        public double FisherF
        {
            get { return FReg; }
        }

        public double CorrelationCoefficient
        {
            get { return RYSQ; }
        }

        public double StandardDeviation
        {
            get { return SDV; }
        }

        public double[] CalculatedValues
        {
            get { return Ycalc; }
        }

        public double[] Residuals
        {
            get { return DY; }
        }

        public double[] Coefficients
        {
            get { return C; }
        }

        public double[] CoefficientsStandardError
        {
            get { return SEC; }
        }

        public double[,] VarianceMatrix
        {
            get { return V; }
        }

        public bool Regress(double[] Y, double[,] X, double[] W)
        {
            int M = Y.Length;             // M = Number of data points
            int N = X.Length / M;         // N = Number of linear terms
            int NDF = M - N;              // Degrees of freedom
            Ycalc = new double[M];
            DY = new double[M];
            // If not enough data, don't attempt regression
            if (NDF < 1)
            {
                return false;
            }
            V = new double[N, N];
            C = new double[N];
            SEC = new double[N];
            double[] B = new double[N];   // Vector for LSQ

            // Clear the matrices to start out
            for (int i = 0; i < N; i++)
                for (int j = 0; j < N; j++)
                    V[i, j] = 0;

            // Form Least Squares Matrix
            for (int i = 0; i < N; i++)
            {
                for (int j = 0; j < N; j++)
                {
                    V[i, j] = 0;
                    for (int k = 0; k < M; k++)
                        V[i, j] = V[i, j] + W[k] * X[i, k] * X[j, k];
                }
                B[i] = 0;
                for (int k = 0; k < M; k++)
                    B[i] = B[i] + W[k] * X[i, k] * Y[k];
            }
            // V now contains the raw least squares matrix
            if (!SymmetricMatrixInvert(V))
            {
                return false;
            }
            // V now contains the inverted least square matrix
            // Matrix multpily to get coefficients C = VB
            for (int i = 0; i < N; i++)
            {
                C[i] = 0;
                for (int j = 0; j < N; j++)
                    C[i] = C[i] + V[i, j] * B[j];
            }

            // Calculate statistics
            double TSS = 0;
            double RSS = 0;
            double YBAR = 0;
            double WSUM = 0;
            for (int k = 0; k < M; k++)
            {
                YBAR = YBAR + W[k] * Y[k];
                WSUM = WSUM + W[k];
            }
            YBAR = YBAR / WSUM;
            for (int k = 0; k < M; k++)
            {
                Ycalc[k] = 0;
                for (int i = 0; i < N; i++)
                    Ycalc[k] = Ycalc[k] + C[i] * X[i, k];
                DY[k] = Ycalc[k] - Y[k];
                TSS = TSS + W[k] * (Y[k] - YBAR) * (Y[k] - YBAR);
                RSS = RSS + W[k] * DY[k] * DY[k];
            }
            double SSQ = RSS / NDF;
            RYSQ = 1 - RSS / TSS;
            FReg = 9999999;
            if (RYSQ < 0.9999999)
                FReg = RYSQ / (1 - RYSQ) * NDF / (N - 1);
            SDV = Math.Sqrt(SSQ);

            // Calculate var-covar matrix and std error of coefficients
            for (int i = 0; i < N; i++)
            {
                for (int j = 0; j < N; j++)
                    V[i, j] = V[i, j] * SSQ;
                SEC[i] = Math.Sqrt(V[i, i]);
            }
            return true;
        }


        public bool SymmetricMatrixInvert(double[,] V)
        {
            int N = (int)Math.Sqrt(V.Length);
            double[] t = new double[N];
            double[] Q = new double[N];
            double[] R = new double[N];
            double AB;
            int K, L, M;

            // Invert a symetric matrix in V
            for (M = 0; M < N; M++)
                R[M] = 1;
            K = 0;
            for (M = 0; M < N; M++)
            {
                double Big = 0;
                for (L = 0; L < N; L++)
                {
                    AB = Math.Abs(V[L, L]);
                    if ((AB > Big) && (R[L] != 0))
                    {
                        Big = AB;
                        K = L;
                    }
                }
                if (Big == 0)
                {
                    return false;
                }
                R[K] = 0;
                Q[K] = 1 / V[K, K];
                t[K] = 1;
                V[K, K] = 0;
                if (K != 0)
                {
                    for (L = 0; L < K; L++)
                    {
                        t[L] = V[L, K];
                        if (R[L] == 0)
                            Q[L] = V[L, K] * Q[K];
                        else
                            Q[L] = -V[L, K] * Q[K];
                        V[L, K] = 0;
                    }
                }
                if ((K + 1) < N)
                {
                    for (L = K + 1; L < N; L++)
                    {
                        if (R[L] != 0)
                            t[L] = V[K, L];
                        else
                            t[L] = -V[K, L];
                        Q[L] = -V[K, L] * Q[K];
                        V[K, L] = 0;
                    }
                }
                for (L = 0; L < N; L++)
                    for (K = L; K < N; K++)
                        V[L, K] = V[L, K] + t[L] * Q[K];
            }
            M = N;
            L = N - 1;
            for (K = 1; K < N; K++)
            {
                M = M - 1;
                L = L - 1;
                for (int J = 0; J <= L; J++)
                    V[M, J] = V[J, M];
            }
            return true;
        }

        public double RunTest(double[] X)
        {
            int NRuns = 1;
            int N1 = 0;
            int N2 = 0;
            if (X[0] > 0)
                N1 = 1;
            else
                N2 = 1;

            for (int k = 1; k < X.Length; k++)
            {
                if (X[k] > 0)
                    N1++;
                else
                    N2++;
                if (X[k] * X[k - 1] < 0)
                    NRuns++;
            }
            return 1;
        }

    }

}

