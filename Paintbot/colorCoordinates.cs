﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Paintbot
{
    class ColorCoordinate
    {
        private int xPos, yPos;

        public ColorCoordinate(int x, int y)
        {
            xPos = x;
            yPos = y;
        }

        public int getXpos()
        {
            return xPos;
        }

        public int getYpos()
        {
            return yPos;
        }

        public double checkDistance(ColorCoordinate pointToCheck)
        {
            double a = Math.Abs(this.xPos - pointToCheck.getXpos());
            double b = Math.Abs(this.yPos - pointToCheck.getYpos());
            double distance = Math.Sqrt(Math.Pow(a, 2) + Math.Pow(b, 2));
            return distance;
        }
    }
}
