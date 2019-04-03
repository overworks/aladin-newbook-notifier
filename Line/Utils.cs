using Line.Messaging;

namespace Mh.Functions.AladinNewBookNotifier.Line
{
    /// <summary>라인 관련 유틸 모음</summary>
    public static class Utils
    {
        public static BubbleContainer ToBubbleContainer(this Aladin.ItemLookUpResult.Item item)
        {
            string linkUrl = Aladin.Utils.UnescapeUrl(item.link);
            string coverUrl = Aladin.Utils.GetHQCoverUrl(item);

            UriTemplateAction linkAction = new UriTemplateAction("상품 페이지로 이동", linkUrl);

            // 이미지
            ImageComponent hero = new ImageComponent(coverUrl);
            hero.Size = ComponentSize.Full;
            hero.AspectMode = AspectMode.Cover;
            //hero.AspectRatio = new AspectRatio(2, 3);
            hero.AspectRatio = AspectRatio._151_1;   // 잘려도 여러개(그래봐야 2개까지지만) 나오는게 낫다고 의견이 나와서
            hero.Action = linkAction;

            // 바디
            // 부제가 있으면 따로 처리를 해준다.
            string titleStr = item.title;
            string subTitleStr = item.subInfo.subTitle;
            if (!string.IsNullOrEmpty(subTitleStr))
            {
                int index = titleStr.IndexOf(subTitleStr);
                if (index >= 0)
                {
                    titleStr = titleStr.Substring(0, index - 3);
                }
            }

            TextComponent title = new TextComponent(titleStr);
            title.Size = ComponentSize.Lg;
            title.Weight = Weight.Bold;
            TextComponent subTitle = null;
            if (!string.IsNullOrEmpty(subTitleStr))
            {
                subTitle = new TextComponent(subTitleStr);
                subTitle.Size = ComponentSize.Sm;
            }
            TextComponent author = new TextComponent(item.author);
            author.Size = ComponentSize.Sm;
            author.Margin = Spacing.Sm;
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
            BoxComponent price = new BoxComponent(BoxLayout.Baseline);
            price.Contents.Add(priceStandard);
            price.Contents.Add(priceSales);
            
            BoxComponent body = new BoxComponent(BoxLayout.Vertical);
            body.Contents.Add(title);
            if (subTitleStr != null)
            {
                body.Contents.Add(subTitle);
            }
            body.Contents.Add(author);
            body.Contents.Add(publisher);
            body.Contents.Add(pubDate);
            body.Contents.Add(price);
            body.Action = linkAction;

            BubbleContainer container = new BubbleContainer();
            container.Hero = hero;
            container.Body = body;

            return container;
        }
    }
}