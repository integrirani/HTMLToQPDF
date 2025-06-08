using HtmlAgilityPack;
using HTMLQuestPDF.Extensions;
using HTMLToQPDF.Components;
using QuestPDF.Fluent;
using QuestPDF.Infrastructure;

namespace HTMLQuestPDF.Components
{
    internal class ParagraphComponent : IComponent
    {
        private readonly List<HtmlNode> lineNodes;
        private readonly Dictionary<string, TextStyle> textStyles;

        public ParagraphComponent(List<HtmlNode> lineNodes, HTMLComponentsArgs args)
        {
            this.lineNodes = lineNodes;
            this.textStyles = args.TextStyles;
        }

        private HtmlNode? GetParrentBlock(HtmlNode node)
        {
            if (node == null) return null;
            return node.IsBlockNode() ? node : GetParrentBlock(node.ParentNode);
        }

        private HtmlNode? GetListItemNode(HtmlNode node)
        {
            if (node == null || node.IsList()) return null;
            return node.IsListItem() ? node : GetListItemNode(node.ParentNode);
        }

        public void Compose(IContainer container)
        {
            var listItemNode = GetListItemNode(lineNodes.First()) ?? GetParrentBlock(lineNodes.First());
            if (listItemNode == null) return;

            var numberInList = listItemNode.GetNumberInList();

            if (numberInList != -1 || listItemNode.GetListNode() != null)
            {
                container.Row(row =>
                {
                    var listPrefix = numberInList == -1 ? "" : numberInList == 0 ? "•  " : $"{numberInList}. ";
                    row.AutoItem().MinWidth(26).AlignCenter().Text(listPrefix);
                    container = row.RelativeItem();
                });
            }

            var first = lineNodes.First();
            var last = lineNodes.First();

            first.InnerHtml = first.InnerHtml.TrimStart();
            last.InnerHtml = last.InnerHtml.TrimEnd();

            container.Text(GetAction(lineNodes));
        }

        private Action<TextDescriptor> GetAction(List<HtmlNode> nodes)
        {
            return text =>
            {
                lineNodes.ForEach(node => GetAction(node).Invoke(text));
            };
        }

        private Action<TextDescriptor> GetAction(HtmlNode node)
        {
            return text =>
            {
                if (node.NodeType == HtmlNodeType.Text)
                {
                    var span = text.Span(node.InnerText);
                    GetTextSpanAction(node).Invoke(span);
                }
                else if (node.IsBr())
                {
                    var span = text.Span("\n");
                    GetTextSpanAction(node).Invoke(span);
                }
                else
                {
                    foreach (var item in node.ChildNodes)
                    {
                        var action = GetAction(item);
                        action(text);
                    }
                }
            };
        }

        private TextSpanAction GetTextSpanAction(HtmlNode node)
        {
            return spanAction =>
            {
                var action = GetTextStyles(node);
                action(spanAction);
                if (node.ParentNode != null)
                {
                    var parrentAction = GetTextSpanAction(node.ParentNode);
                    parrentAction(spanAction);
                }
            };
        }

        public TextSpanAction GetTextStyles(HtmlNode element)
        {
            return (span) => span.Style(GetTextStyle(element));
        }

        public TextStyle GetTextStyle(HtmlNode element)
        {
            var textStyle = textStyles.TryGetValue(element.Name.ToLower(), out TextStyle? style) ? style : TextStyle.Default;

            var styleAttribute = element.GetAttributeValue("style", "").ToLowerInvariant();
            var styleAttributes = styleAttribute.Split(';', StringSplitOptions.RemoveEmptyEntries);
            Dictionary<string, string> styles = new Dictionary<string, string>();

            foreach (var attr in styleAttributes)
            {
                var parts = attr.Split(':', StringSplitOptions.RemoveEmptyEntries);
                if (parts.Length == 2)
                {
                    styles[parts[0].Trim()] = parts[1].Trim();
                }
            }

            var fontWeight = styles.GetValueOrDefault("font-weight", "").ToLowerInvariant();

            if (fontWeight == "bold")
            {
                textStyle = textStyle.Bold();
            }

            var fontStyle = styles.GetValueOrDefault("font-style", "").ToLowerInvariant();

            if (fontStyle == "italic")
            {
                textStyle = textStyle.Italic();
            }

            var fontFamily = styles.GetValueOrDefault("font-family", "").ToLowerInvariant();

            if (!string.IsNullOrEmpty(fontFamily))
            {
                textStyle = textStyle.FontFamily(fontFamily);
            }

            var color = styles.GetValueOrDefault("color", "").ToLowerInvariant();

            var rgbMatch = System.Text.RegularExpressions.Regex.Match(color, @"rgb\(\s*(\d+)\s*,\s*(\d+)\s*,\s*(\d+)\s*\)");

            if (rgbMatch.Success)
            {
                var r = int.Parse(rgbMatch.Groups[1].Value);
                var g = int.Parse(rgbMatch.Groups[2].Value);
                var b = int.Parse(rgbMatch.Groups[3].Value);

                textStyle = textStyle.FontColor(RgbToHex(r, g, b));
            }

            var backgroundColor = styles.GetValueOrDefault("background-color", "").ToLowerInvariant();
            rgbMatch = System.Text.RegularExpressions.Regex.Match(backgroundColor, @"rgb\(\s*(\d+)\s*,\s*(\d+)\s*,\s*(\d+)\s*\)");

            if (rgbMatch.Success)
            {
                var r = int.Parse(rgbMatch.Groups[1].Value);
                var g = int.Parse(rgbMatch.Groups[2].Value);
                var b = int.Parse(rgbMatch.Groups[3].Value);

                textStyle = textStyle.BackgroundColor(RgbToHex(r, g, b));
            }

            var textDecorationLine = styles.GetValueOrDefault("text-decoration-line", "").ToLowerInvariant();

            if (textDecorationLine == "underline")
            {
                textStyle = textStyle.Underline();
            }
            else if (textDecorationLine == "line-through")
            {
                textStyle = textStyle.Strikethrough();
            }

            var fontSize = styles.GetValueOrDefault("font-size", "").ToLowerInvariant();

            if (int.TryParse(fontSize, out int fontSizeValue))
            {
                textStyle = textStyle.FontSize(fontSizeValue);
            }
            else if (fontSize == "x-small")
            {
                textStyle = textStyle.FontSize(7f);
            }
            else if (fontSize == "medium")
            {
                textStyle = textStyle.FontSize(13f);
            }
            else if (fontSize == "large")
            {
                textStyle = textStyle.FontSize(14f);
            }
            else if (fontSize == "larger")
            {
                textStyle = textStyle.FontSize(16f);
            }
            else if (fontSize == "x-large")
            {
                textStyle = textStyle.FontSize(18f);
            }
            else if (fontSize == "xx-large")
            {
                textStyle = textStyle.FontSize(24f);
            }
            else if (fontSize == "xxx-large")
            {
                textStyle = textStyle.FontSize(48f);
            }

            return textStyle;
        }
        private static string RgbToHex(int r, int g, int b)
        {
            return $"#{r:X2}{g:X2}{b:X2}";
        }
    }
}