using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace AdHocBaseApp
{
    public class Shape
    {
        //TODO
    }
    public class PointShape : Shape
    {
        public double x;
        public double y;
        public PointShape(double x, double y)
        {
            this.x = x;
            this.y = y;
        }

        public override string ToString()
        {
            return string.Format("({0},{1})", this.x, this.y) ;
        }
    }
}
