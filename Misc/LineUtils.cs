using Line.Messaging;

namespace Mh.Functions.AladinNewBookNotifier
{
    /// <summary>라인 관련 유틸 모음</summary>
    public static class LineUtils
    {
        public static BubbleContainerFlexMessage MakeBookMessage(ItemLookUpResult.Item item)
        {
            string linkUrl = AladinUtils.UnescapeUrl(item.link);
            string coverUrl = AladinUtils.GetHQCoverUrl(item);

            // 이미지
            ImageComponent hero = new ImageComponent(coverUrl);
            hero.Size = ComponentSize.Full;
            hero.AspectMode = AspectMode.Cover;
            hero.AspectRatio = new AspectRatio(2, 3);
            hero.Action = new UriTemplateAction("상품 페이지로 이동", linkUrl);

            // 바디
            TextComponent title = new TextComponent(item.title);
            title.Size = ComponentSize.Xl;
            title.Weight = Weight.Bold;
            TextComponent author = new TextComponent(item.author);
            author.Size = ComponentSize.Sm;
            TextComponent publisher = new TextComponent(item.publisher);
            publisher.Size = ComponentSize.Sm;
            TextComponent pubDate = new TextComponent(item.pubDate);
            pubDate.Size = ComponentSize.Sm;
            TextComponent priceStandard = new TextComponent($"정가 {item.priceStandard}원 / ");
            priceStandard.Size = ComponentSize.Sm;
            priceStandard.Flex = 0;
            TextComponent priceSales = new TextComponent($"판매가 {item.priceSales}원 ");
            priceSales.Size = ComponentSize.Sm;
            priceSales.Weight = Weight.Bold;
            priceSales.Flex = 0;
            // TextComponent mileage = new TextComponent($"마일리지 {item.mileage}점");
            // mileage.Size = ComponentSize.Sm;
            // mileage.Color = "#888888";
            // mileage.Flex = 0;
            BoxComponent price = new BoxComponent(BoxLayout.Baseline);
            price.Contents.Add(priceStandard);
            price.Contents.Add(priceSales);
            //price.Contents.Add(mileage);            
            
            BoxComponent body = new BoxComponent(BoxLayout.Vertical);
            body.Contents.Add(title);
            body.Contents.Add(author);
            body.Contents.Add(publisher);
            body.Contents.Add(pubDate);
            body.Contents.Add(price);

            // 푸터
            ButtonComponent linkButton = new ButtonComponent();
            linkButton.Style = ButtonStyle.Secondary;
            linkButton.Action = new UriTemplateAction("상품 페이지로 이동", linkUrl);
            
            BoxComponent footer = new BoxComponent();
            footer.Contents.Add(linkButton);
            
            BubbleContainer container = new BubbleContainer();
            container.Hero = hero;
            container.Body = body;
            container.Footer = footer;

            string altText = $"{item.title} ({item.author} / {item.publisher} / {item.pubDate} / {item.priceStandard}원)";

            return FlexMessage.CreateBubbleMessage(altText).SetBubbleContainer(container);
        }
    }
}