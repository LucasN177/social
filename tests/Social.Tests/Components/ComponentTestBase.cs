using System.Globalization;
using Bunit;
using Microsoft.Extensions.DependencyInjection;

namespace Social.Tests.Components;

/// <summary>bUnit-Kontext mit den Diensten, die jede Komponente braucht (Localizer aus _Imports.razor).</summary>
public abstract class ComponentTestBase : BunitContext
{
    protected ComponentTestBase()
    {
        // Englisch = Schlüssel, so sind die Tests unabhängig von der Rechner-Sprache
        CultureInfo.CurrentUICulture = CultureInfo.GetCultureInfo("en-US");
        Services.AddLocalization();
    }
}
