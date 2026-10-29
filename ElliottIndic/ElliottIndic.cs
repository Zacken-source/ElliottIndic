using System;
using System.Collections.Generic;
using cAlgo.API;
using cAlgo.API.Indicators;
using cAlgo.API.Internals;

namespace cAlgo
{
    // Tries to find the last Elliott impulse (0-1-2-3-4-5) and its A-B-C correction
    // on a zigzag. It only checks the basic rules, so it's a hint for the trend,
    // not something to trade on alone.
    [Indicator(IsOverlay = true, TimeZone = TimeZones.UTC, AccessRights = AccessRights.None)]
    public class ElliottIndic : Indicator
    {
        [Parameter("Zigzag size (ATR)", DefaultValue = 2.0, MinValue = 0.5)]
        public double ZigzagAtr { get; set; }

        [Parameter("Bars to scan", DefaultValue = 1500, MinValue = 200)]
        public int BarsToScan { get; set; }

        [Parameter("Allow wave 4 overlap", DefaultValue = false)]
        public bool AllowOverlap { get; set; }

        [Parameter("Show zigzag", DefaultValue = false)]
        public bool ShowZigzag { get; set; }

        [Parameter("Wave color", DefaultValue = "Orange")]
        public Color WaveColor { get; set; }

        [Parameter("Zigzag color", DefaultValue = "DimGray")]
        public Color ZigzagColor { get; set; }

        private class Point
        {
            public int Index;
            public double Price;
            public bool IsHigh;
        }

        private AverageTrueRange atr;
        private List<string> objects = new List<string>();
        private int lastIndex = -1;
        private int counter = 0;

        protected override void Initialize()
        {
            atr = Indicators.AverageTrueRange(14, MovingAverageType.Simple);
        }

        public override void Calculate(int index)
        {
            if (!IsLastBar || index == lastIndex)
                return;
            lastIndex = index;

            foreach (var name in objects)
                Chart.RemoveObject(name);
            objects.Clear();

            var points = Zigzag(index - 1);
            if (ShowZigzag)
            {
                for (int i = 1; i < points.Count; i++)
                    Chart.DrawTrendLine(NewName(), points[i - 1].Index, points[i - 1].Price, points[i].Index, points[i].Price, ZigzagColor, 1, LineStyle.Dots);
            }

            // look for the most recent valid impulse
            for (int end = points.Count - 1; end >= 5; end--)
            {
                if (IsImpulse(points, end - 5))
                {
                    DrawCount(points, end - 5);
                    return;
                }
            }

            Chart.DrawStaticText(NewName(), "Elliott: no impulse found", VerticalAlignment.Top, HorizontalAlignment.Left, Color.Gray);
        }

        // Zigzag: a new point is added when price comes back by ZigzagAtr * ATR.
        // The last point is the current extreme, it can still move.
        private List<Point> Zigzag(int last)
        {
            var points = new List<Point>();
            int first = Math.Max(20, last - BarsToScan);
            int direction = 0;
            int highIndex = first, lowIndex = first;
            double high = Bars.HighPrices[first], low = Bars.LowPrices[first];

            for (int i = first + 1; i <= last; i++)
            {
                double size = ZigzagAtr * atr.Result[i];

                if (direction >= 0 && Bars.HighPrices[i] > high)
                {
                    high = Bars.HighPrices[i];
                    highIndex = i;
                }
                if (direction <= 0 && Bars.LowPrices[i] < low)
                {
                    low = Bars.LowPrices[i];
                    lowIndex = i;
                }

                if (direction >= 0 && high - Bars.LowPrices[i] >= size && i > highIndex)
                {
                    points.Add(new Point { Index = highIndex, Price = high, IsHigh = true });
                    direction = -1;
                    low = Bars.LowPrices[i];
                    lowIndex = i;
                }
                else if (direction <= 0 && Bars.HighPrices[i] - low >= size && i > lowIndex)
                {
                    points.Add(new Point { Index = lowIndex, Price = low, IsHigh = false });
                    direction = 1;
                    high = Bars.HighPrices[i];
                    highIndex = i;
                }
            }

            if (direction == 1)
                points.Add(new Point { Index = highIndex, Price = high, IsHigh = true });
            else if (direction == -1)
                points.Add(new Point { Index = lowIndex, Price = low, IsHigh = false });

            return points;
        }

        private bool IsImpulse(List<Point> p, int start)
        {
            // flip the prices of a bearish impulse so the checks are the same
            double sign = p[start].IsHigh ? -1 : 1;
            double w0 = sign * p[start].Price;
            double w1 = sign * p[start + 1].Price;
            double w2 = sign * p[start + 2].Price;
            double w3 = sign * p[start + 3].Price;
            double w4 = sign * p[start + 4].Price;
            double w5 = sign * p[start + 5].Price;

            // wave 2 doesn't go below the start of wave 1
            if (w2 <= w0)
                return false;
            // 3 goes above 1, 5 goes above 3
            if (w3 <= w1 || w5 <= w3)
                return false;
            // wave 4 doesn't go into wave 1
            if (!AllowOverlap && w4 <= w1)
                return false;
            if (w4 <= w2)
                return false;

            // wave 3 is never the shortest
            double len1 = w1 - w0;
            double len3 = w3 - w2;
            double len5 = w5 - w4;
            if (len3 < len1 && len3 < len5)
                return false;

            return true;
        }

        private void DrawCount(List<Point> p, int start)
        {
            var labels = new List<string> { "0", "1", "2", "3", "4", "5" };
            int count = 6;
            double sign = p[start].IsHigh ? -1 : 1;
            double top5 = sign * p[start + 5].Price;

            // A-B-C after wave 5 if the points are there
            if (start + 6 < p.Count)
            {
                labels.Add("A");
                count++;
                double a = sign * p[start + 6].Price;
                if (start + 7 < p.Count && sign * p[start + 7].Price < top5)
                {
                    labels.Add("B");
                    count++;
                    if (start + 8 < p.Count && sign * p[start + 8].Price < a)
                    {
                        labels.Add("C");
                        count++;
                    }
                }
            }

            for (int i = 0; i < count; i++)
            {
                var point = p[start + i];
                if (i > 0)
                {
                    var prev = p[start + i - 1];
                    Chart.DrawTrendLine(NewName(), prev.Index, prev.Price, point.Index, point.Price, WaveColor, 2, LineStyle.Solid);
                }
                var text = Chart.DrawText(NewName(), labels[i], point.Index, point.Price, WaveColor);
                text.HorizontalAlignment = HorizontalAlignment.Center;
                text.VerticalAlignment = point.IsHigh ? VerticalAlignment.Top : VerticalAlignment.Bottom;
            }

            // what should come next
            string lastLabel = labels[count - 1];
            string next = "wave A";
            if (lastLabel == "A") next = "wave B";
            else if (lastLabel == "B") next = "wave C";
            else if (lastLabel == "C") next = "new impulse";

            string trend = sign > 0 ? "bullish" : "bearish";
            Chart.DrawStaticText(NewName(), "Elliott: " + trend + " impulse, next: " + next + " (hint only)",
                VerticalAlignment.Top, HorizontalAlignment.Left, WaveColor);
        }

        private string NewName()
        {
            string name = "elliott_" + counter++;
            objects.Add(name);
            return name;
        }
    }
}
