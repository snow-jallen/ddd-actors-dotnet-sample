namespace Ohmline.Mobile.Views;

/// <summary>
/// Shows a union state with the template its <see cref="Templates"/> choose for its case, bound
/// to that case: a <c>Failed</c> template binds to the failure's message. No state, no content.
/// </summary>
public sealed class StateView : ContentView
{
    public static readonly BindableProperty StateProperty = BindableProperty.Create(
        nameof(State), typeof(object), typeof(StateView), propertyChanged: Render);

    public static readonly BindableProperty TemplatesProperty = BindableProperty.Create(
        nameof(Templates), typeof(IStateTemplates), typeof(StateView), propertyChanged: Render);

    /// <summary>The union value to show, such as the view model's <c>Screen</c>.</summary>
    public object? State
    {
        get => GetValue(StateProperty);
        set => SetValue(StateProperty, value);
    }

    public IStateTemplates? Templates
    {
        get => (IStateTemplates?)GetValue(TemplatesProperty);
        set => SetValue(TemplatesProperty, value);
    }

    private static void Render(BindableObject bindable, object? before, object? after)
    {
        var view = (StateView)bindable;
        if (view.State is not { } state || view.Templates is not { } templates)
        {
            view.Content = null;
            return;
        }

        var (template, @case) = templates.Select(state);
        var content = (View)template.CreateContent();
        content.BindingContext = @case;
        view.Content = content;
    }
}
