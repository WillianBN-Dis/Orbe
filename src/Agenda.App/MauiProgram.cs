using Microsoft.Extensions.Logging;

namespace Agenda.App;

public static class MauiProgram
{
	public static MauiApp CreateMauiApp()
	{
		var builder = MauiApp.CreateBuilder();
#if ANDROID
        InstantInteraction.Configure();
#endif
#if WINDOWS
        DesktopPointer.Configure();WorkspaceFields.Configure();
#endif
		builder
			.UseMauiApp<App>()
            .ConfigureMauiHandlers(handlers => handlers.AddHandler<DayTimelineScrollView, DayTimelineScrollHandler>())
			.ConfigureFonts(fonts =>
			{
				fonts.AddFont("OpenSans-Regular.ttf", "OpenSansRegular");
				fonts.AddFont("OpenSans-Semibold.ttf", "OpenSansSemibold");
			});

#if DEBUG
		builder.Logging.AddDebug();
#endif

		return builder.Build();
	}
}
