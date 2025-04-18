using System.Windows;
using System.Windows.Documents;
using System.Windows.Media;
using Guardian.Models;
using Guardian.ViewModels;

namespace Guardian.Views;

public partial class MainView 
{
    public MainView(MainViewModel viewModel)
    {
        InitializeComponent();
        DataContext = viewModel;
        
        viewModel.UrlFiltered += OnUrlFiltered;
        
        Loaded += OnLoaded;
    }

    private void OnLoaded(object sender, RoutedEventArgs e)
    {
       RequestsLog.Document = new FlowDocument();
    }

    private void OnUrlFiltered(object? sender, FilterResponse response)
    {
        /*
         *  <FlowDocument FontSize="13" FontFamily="{Binding RelativeSource={RelativeSource FindAncestor, AncestorType={x:Type ui:FluentWindow}, AncestorLevel=1}, Path=FontFamily}" LineHeight="0.2">
                    <Paragraph  TextAlignment="Left" Foreground="#f22c3d">
                        <Span>B | </Span>
                        <Span>854us | </Span>
                        <Span>https://chatgpt.com/c/6802b76f-151c-8011-83d155</Span>
                    </Paragraph>
                    <Paragraph TextAlignment="Left" Foreground="#00a67d">
                        <Span>P | </Span>
                        <Span>854us | </Span>
                        <Span>https://chatgpt.com/c/6802b76f-151c-8011-83d155</Span>
                    </Paragraph>
                </FlowDocument>
         */

        Dispatcher.BeginInvoke(() =>
        {
            var document = RequestsLog.Document;
            var paragraph = new Paragraph
            {
                TextAlignment = TextAlignment.Left,
                Foreground = response.IsBlocked ? Brushes.Red : Brushes.Green
            };

            var span = new Span
            {
                Foreground = response.IsBlocked ? Brushes.Red : Brushes.Green,
                FontSize = 13,
                FontFamily = new FontFamily("Segoe UI")
            };

            span.Inlines.Add(new Run(response.IsBlocked ? "B | " : "P | "));
            span.Inlines.Add(new Run($"{response.TookUs}us | "));
            span.Inlines.Add(new Run(response.Url));
            paragraph.Inlines.Add(span);
            document.Blocks.Add(paragraph);
            RequestsLog.ScrollToEnd();
        });
    }
}