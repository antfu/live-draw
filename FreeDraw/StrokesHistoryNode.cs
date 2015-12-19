using System.Windows.Ink;

namespace AntFu7.FreeDraw
{
    public enum StrokesHistoryNodeType
    {
        Removed,
        Added
    }
    internal class StrokesHistoryNode
    {
        public StrokeCollection Strokes { get; private set; }
        public StrokesHistoryNodeType Type { get; private set; }

        public StrokesHistoryNode(StrokeCollection strokes, StrokesHistoryNodeType type)
        {
            Strokes = strokes;
            Type = type;
        }
    }
}
