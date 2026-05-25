using System.Collections;
using HandlebarsDotNet;

namespace RepoManager.Infrastructure.Services.Handlebars;

public static class HandlebarsHelpers
{
    public static void Register(IHandlebars hbs)
    {
        hbs.RegisterHelper("formatDate", (output, context, args) =>
        {
            if (args.Length < 1) return;
            var format = args.Length >= 2 ? args[1]?.ToString() ?? "yyyy-MM-dd" : "yyyy-MM-dd";
            if (args[0] is DateTimeOffset dto)
                output.WriteSafeString(dto.ToString(format));
            else if (DateTimeOffset.TryParse(args[0]?.ToString(), out var parsed))
                output.WriteSafeString(parsed.ToString(format));
        });

        hbs.RegisterHelper("length", (output, context, args) =>
        {
            if (args.Length < 1) { output.WriteSafeString("0"); return; }
            var count = args[0] switch
            {
                ICollection c => c.Count,
                IEnumerable e => e.Cast<object>().Count(),
                _ => 0
            };
            output.WriteSafeString(count.ToString());
        });

        hbs.RegisterHelper("eq", (context, args) =>
            args.Length >= 2 && string.Equals(args[0]?.ToString(), args[1]?.ToString(), StringComparison.Ordinal));

        hbs.RegisterHelper("gt", (context, args) =>
        {
            if (args.Length < 2) return false;
            return double.TryParse(args[0]?.ToString(), out var a)
                && double.TryParse(args[1]?.ToString(), out var b)
                && a > b;
        });

        hbs.RegisterHelper("minus", (output, context, args) =>
        {
            if (args.Length < 2) { output.WriteSafeString("0"); return; }
            if (double.TryParse(args[0]?.ToString(), out var a)
             && double.TryParse(args[1]?.ToString(), out var b))
                output.WriteSafeString((a - b).ToString());
        });

        hbs.RegisterHelper("lower", (output, context, args) =>
        {
            if (args.Length >= 1)
                output.WriteSafeString(args[0]?.ToString()?.ToLowerInvariant() ?? string.Empty);
        });

        hbs.RegisterHelper("upper", (output, context, args) =>
        {
            if (args.Length >= 1)
                output.WriteSafeString(args[0]?.ToString()?.ToUpperInvariant() ?? string.Empty);
        });

        hbs.RegisterHelper("truncate", (output, context, args) =>
        {
            if (args.Length < 1) return;
            var s = args[0]?.ToString() ?? string.Empty;
            var max = args.Length >= 2 && int.TryParse(args[1]?.ToString(), out var m) ? m : 80;
            output.WriteSafeString(s.Length <= max ? s : string.Concat(s.AsSpan(0, max), "…"));
        });

        hbs.RegisterHelper("jiraLink", (output, context, args) =>
        {
            if (args.Length < 1) return;
            var ticketId = args[0]?.ToString() ?? string.Empty;
            output.WriteSafeString($"[{ticketId}]");
        });
    }
}
