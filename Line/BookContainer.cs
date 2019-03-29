using Line.Messaging;

namespace Mh.Functions.AladinNewBookNotifier
{
    public class BookContainer : IFlexContainer
    {
        public FlexContainerType Type { get { return FlexContainerType.Bubble; } }
    }
}