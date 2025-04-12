using Guardian.ViewModels;

namespace Guardian.Views;

public partial class MainView 
{
    public MainView(MainViewModel viewModel)
    {
        InitializeComponent();
        DataContext = viewModel;
    }
}