namespace EclipsedClient.ViewModels;

internal sealed class DiscoverViewModel(HomeViewModel home, ChartsViewModel charts) : ViewModel
{
    public HomeViewModel Home { get; } = home;
    public ChartsViewModel Charts { get; } = charts;
}
