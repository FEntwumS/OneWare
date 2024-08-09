using Avalonia.Media;

namespace OneWare.UniversalFpgaProjectSystem.ViewModels.FpgaGuiElements;

public class FpgaGuiElementImageViewModel : FpgaGuiElementRectViewModel
{
    public IImage? Image { get; init; }
    
    public FpgaGuiElementImageViewModel(double x, double y, double width, double height) : base(x, y, width, height)
    {
        
    }
}