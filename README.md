# Smart Home – Making Everyday Life a Little Easier!

A personal smart home dashboard built with .NET and Blazor, designed to run on a wall-mounted touchscreen in the hallway, powered by a Raspberry Pi.

## Background

The screen is intended to serve as a central hub for both smart home devices and the information that actually matters when you're heading out the door.

The main goal of this project is to be **self-contained and fully configurable**. Users should be able to decide what appears on the dashboard without having to modify the code, rebuild the project, and deploy a new version every time. Everything is configured directly through the user interface.

The Settings page allows you to configure:

- Weather and location
- World clocks
- RSS and news feeds
- Financial news, electricity prices, fuel prices, interest rates, and stocks
- School lunch menus
- Private calendars
- Bus departures
- Smart home devices, such as Shelly

All settings are stored locally in `BLZULSA/data/settings.json`. This file is automatically created on the first launch and remains on the local installation. It should never be version-controlled or published with the project.

## Why I Built This

I've never been a big fan of relying on ready-made smart home dashboards and templates, such as those offered by Home Assistant. Not because they don't work, but because I enjoy building things myself.

Why adapt to someone else's system when you can create your own?

I wanted something independent, built from scratch, where I have full control over the design, functionality, integrations, and how everything works together.

The goal was never to create another Home Assistant. It was to build my own solution, tailored to my home, my needs, and the way I want things to work.

For me, the fun is just as much in developing, experimenting, solving problems, and learning new things as it is in using the finished dashboard.

No predefined templates. No unnecessary complexity. Just my own ideas, my own code, and a Raspberry Pi on the wall.

## Dashboard Tabs

| **Tab** | **Content** |
| --- | --- |
| Home | Clock, weather, name days, local news, and world clocks |
| Smart Home | Control devices, view indoor sensor readings |
| Finance | Hourly electricity prices, fuel prices, interest rates, stocks, and financial news |
| Planning | Calendar, bus departures, and school lunch menus |
| Settings | All configuration options, plus a health monitoring view for data sources |

## Features

**Smart Home.** Shelly devices can be added directly through Settings. The dashboard displays their current status and allows you to control them. It also alerts you when required information is missing, such as a device ID.

**News.** RSS feeds are configured by the user. All 21 regional news divisions of Sweden's public broadcaster, SVT, are available in a dropdown menu. Simply select Örebro, Stockholm, or your own region without needing to know the feed URL. Any other RSS feed can also be added.

**Weather.** Search for a city or location, and the coordinates and time zone are automatically filled in.

**World Clocks.** Choose which cities to display, or disable the entire section.

**Fuel Prices.** Select a county and then one or more municipalities to display the cheapest fuel station for each fuel type, including its address.

**School Lunch.** Select a county, municipality, and school from dropdown lists. Menus are retrieved using a headless browser.

**Calendar.** Add private calendars using their ICS URLs. Recurring events and all-day events are handled correctly.

**Public Transport.** Search for a bus stop and view upcoming departures, including countdown timers and delays.

**Health Monitoring.** A dedicated view shows when each data source was last successfully updated, along with a log of warnings and errors. A wall-mounted screen cannot tell you when something goes wrong. If an API key expires, outdated information may remain on the screen and appear perfectly normal. This is where those problems become visible.

## Getting Started

Requires the **.NET 10 SDK**.

1. Clone or download the repository.
2. Open the solution in Visual Studio, or run `dotnet run` from the `BLZULSA` directory.
3. Set up your PIN, then open Settings to configure your dashboard.

On the first launch, `data/settings.json` is generated from `dashboard.defaults.json`. The default configuration contains no API keys or personal information. Everything is configured through the user interface.

### Setting Up Your PIN

Settings requires a four-digit PIN, which must be configured through a terminal before first use.

**Development:**
```bash
dotnet run --project BLZULSA -- --satt-pin
```

**Raspberry Pi:**
```bash
cd /opt/ulsavilagatan/nuvarande
SettingsFile=/opt/ulsavilagatan/data/settings.json ./BLZULSA --satt-pin
```

Enter and confirm your PIN. It takes effect immediately.

To change your PIN, run the same command again.
### API Keys

Most data sources are publicly available and require no authentication. The following services require an API key or authorization:

| **Service** | **Where to Get the Key** | **Cost** |
| --- | --- | --- |
| Weather | [openweathermap.org](https://openweathermap.org/) | Free |
| Stocks | [finnhub.io](https://finnhub.io/) | Free |
| Bus Departures | [trafiklab.se](https://www.trafiklab.se/), project *ResRobot – Reseplanerare* | Free |
| Smart Home | Shelly Cloud → User Settings → Authorization Information | Included with your account |

Public data sources that do not require API keys include electricity prices (elprisetjustnu.se), fuel prices (henrikhjelm.se), interest rates (the Riksbank SWEA API), Swedish name days (sholiday.faboul.se), and all RSS feeds.

### Refresh Intervals

Each data source has its own configurable refresh interval under General Settings, with a minimum interval that cannot be reduced.

These minimum intervals exist to protect the APIs, as several providers enforce rate limits when requests are made too frequently.

| **Data Source** | **Minimum Interval** | **Data Source** | **Minimum Interval** |
| --- | --- | --- | --- |
| Weather, name days, electricity prices, fuel prices | 60 min | Bus departures | 2 min |
| News, financial news, stocks, calendar | 15 min | Smart home devices | 15 sec |
| Interest rates, school lunch | 6 hours | Indoor sensors | 5 min |

The minimum intervals are enforced both in the user interface and in the background services, so they cannot be bypassed by manually editing `settings.json`.

## Security

The Settings page is protected by a four-digit PIN configured through the terminal. For security reasons, the dashboard should only be used on a trusted local network and should not be exposed directly to the internet.

API keys are stored in plain text in `data/settings.json`. This file is excluded by `.gitignore` and should never be committed or uploaded to a public repository.

However, it is worth backing up securely, as it contains all your configuration settings.

## Raspberry Pi

Publish the application for a 64-bit Raspberry Pi OS installation:

```bash
dotnet publish -c Release -r linux-arm64 --self-contained false
```

Three important things to keep in mind:

**1. File permissions**

The application directory must be **writable** by the user running the service. Otherwise, saving settings will fail. Place the application inside the user's home directory or assign the appropriate ownership and permissions.

**2. Remote configuration**

Set `ASPNETCORE_URLS=http://0.0.0.0:5266` if you want to access Settings from another device on your local network.

Typing a long ICS URL on a touchscreen without a keyboard is hardly anyone's idea of fun. Configure everything from your laptop and let the wall-mounted display do what it does best: display information.

Remember that this also exposes the application to other devices on the network, so only use this option on a trusted network.

**3. Globalization support**

`InvariantGlobalization` is intentionally disabled, meaning **ICU must be installed** on the system.

ICU is normally available on Raspberry Pi OS, but it may be missing from minimal container images.

Without it, localized dates such as "Söndag 7 september" may suddenly appear as "Sunday September 7".

## Built With

- **C#** – Application logic and backend services
- **Blazor Server (.NET 10)** – Interactive user interface
- **CSS** – Layout and styling
- **REST APIs** – External data integrations
- **RSS** – News feeds
- **JSON** – Local configuration storage
- **Material Symbols** – Icons
- **Ical.Net** – Calendar and recurring event parsing
- **GeoTimeZone** – Offline time zone lookup
- **Headless Browser Automation** – Retrieves school lunch menus directly from the website without requiring an API key.

## About AI Assistance

This project was developed with assistance from Claude Opus, which was used for code suggestions, analysis, debugging, and architectural discussions.

The project's structure, features, and technical solutions have subsequently been adapted, refined, and tested as part of the development process.

## Samples
<img width="1912" height="1074" alt="647344844-41f6ea56-ea91-4e89-ab81-df3612693e4b" src="https://github.com/user-attachments/assets/569b1d26-4e56-4aaf-af4f-c477e8a0c350" />
<img width="1914" height="1077" alt="647344980-8046cdbc-a372-42e2-ba23-fe7edf1ddf20" src="https://github.com/user-attachments/assets/56840e94-2310-4032-851a-e983d8466bbb" />
<img width="1923" height="1078" alt="647345164-52c65241-4693-4535-93ae-1ac82efb67fe" src="https://github.com/user-attachments/assets/d3bddaee-9642-41cb-8227-6f75326361f4" />

