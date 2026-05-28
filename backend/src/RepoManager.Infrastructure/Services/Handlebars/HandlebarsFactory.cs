using HandlebarsDotNet;

namespace RepoManager.Infrastructure.Services.Handlebars;

public static class HandlebarsFactory
{
    public static IHandlebars Create(MissingTokenRecorder recorder)
    {
        var hbs = HandlebarsDotNet.Handlebars.Create();
        hbs.Configuration.FormatterProviders.Add(recorder);
        HandlebarsHelpers.Register(hbs);
        return hbs;
    }
}
