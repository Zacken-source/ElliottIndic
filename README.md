# ElliottIndic

cTrader indicator that looks for the last Elliott impulse (0-1-2-3-4-5) and the A-B-C correction after it, on an ATR zigzag.

Rules checked:
- wave 2 doesn't go below the start of wave 1
- wave 3 is never the shortest
- wave 4 doesn't overlap wave 1 (can be turned off)

It's only a hint for the trend, the count can change when new swings appear.

## Install

In cTrader: Algo > Indicators > New, name it `ElliottIndic`, paste `ElliottIndic/ElliottIndic.cs` and build.
