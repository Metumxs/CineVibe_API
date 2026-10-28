namespace CineVibe.API.Constants
{
    public static class RouletteConstants
    {

        public static readonly List<List<RouletteQuestion>> QuestionBanks = new()
        {
            new List<RouletteQuestion>
            {
                new("genre_q1", "genre", "What's going in your mug tonight?", new()
                {
                    new("opt_1", "☕ Double Espresso (Wake me up)", new(GenreNames: new() { "Action", "Thriller" })),
                    new("opt_2", "🍷 A glass of red wine", new(GenreNames: new() { "Drama", "Mystery" })),
                    new("opt_3", "🫖 Warm chamomile tea", new(GenreNames: new() { "Comedy", "Family" })),
                    new("opt_4", "🍺 Ice cold beer", new(GenreNames: new() { "Comedy", "Action" })),
                    new("opt_5", "🩸 Something stronger...", new(GenreNames: new() { "Horror", "Thriller" })),
                    new("opt_6", "🧪 A glowing neon potion", new(GenreNames: new() { "Science Fiction", "Fantasy" }))
                }),
                new("genre_q2", "genre", "Choose your perfect weather outside:", new()
                {
                    new("opt_1", "⛈️ Heavy thunder and lightning", new(GenreNames: new() { "Action", "Thriller", "Horror" })),
                    new("opt_2", "🌧️ Melancholic rainy evening", new(GenreNames: new() { "Drama", "Mystery" })),
                    new("opt_3", "☀️ Bright warm sun", new(GenreNames: new() { "Comedy", "Adventure" })),
                    new("opt_4", "❄️ Quiet snowy night", new(GenreNames: new() { "Drama", "Romance" })),
                    new("opt_5", "🌪️ An apocalyptic tornado", new(GenreNames: new() { "Action", "Science Fiction" }))
                }),
                new("genre_q3", "genre", "What's the ideal weekend plan?", new()
                {
                    new("opt_1", "🎢 Going to a theme park", new(GenreNames: new() { "Adventure", "Action" })),
                    new("opt_2", "🏛️ Visiting an art museum", new(GenreNames: new() { "Drama", "History" })),
                    new("opt_3", "👻 Exploring an abandoned house", new(GenreNames: new() { "Horror", "Mystery" })),
                    new("opt_4", "🛌 Staying in bed all day", new(GenreNames: new() { "Comedy", "Romance" })),
                    new("opt_5", "🚀 Building a spaceship in the garage", new(GenreNames: new() { "Science Fiction" }))
                }),
                new("genre_q4", "genre", "What's your emotional capacity today?", new()
                {
                    new("opt_1", "🎈 Keep it light and easy", new(GenreNames: new() { "Comedy", "Family" })),
                    new("opt_2", "😭 I'm ready to shed a tear", new(GenreNames: new() { "Drama" })),
                    new("opt_3", "🧠 Melt my brain, please", new(GenreNames: new() { "Mystery", "Science Fiction" })),
                    new("opt_4", "😱 Scare me out of my mind", new(GenreNames: new() { "Horror" }))
                })
            },

            new List<RouletteQuestion>
            {
                new("era_q1", "era", "You open a time capsule. What's inside?", new()
                {
                    new("opt_1", "📸 A dusty black & white photo", new(Era: "classic")),
                    new("opt_2", "🪩 A shiny disco ball", new(Era: "1970s")),
                    new("opt_3", "🎹 A neon synth keyboard", new(Era: "1980s")),
                    new("opt_4", "📼 A tangled VHS tape", new(Era: "1990s")),
                    new("opt_5", "💿 A binder full of burned CDs", new(Era: "2000s")),
                    new("opt_6", "📱 The first touchscreen phone", new(Era: "2010s")),
                    new("opt_7", "🥽 A brand new VR headset", new(Era: "2020s"))
                }),
                new("era_q2", "era", "Pick an aesthetic gadget:", new()
                {
                    new("opt_1", "📻 A dusty wooden radio", new(Era: "classic")),
                    new("opt_2", "📼 A chunky VCR player", new(Era: "1990s")),
                    new("opt_3", "🕹️ An arcade cabinet", new(Era: "1980s")),
                    new("opt_4", "🎧 A glowing VR headset", new(Era: "2020s")),
                    new("opt_5", "📱 The first touchscreen phone", new(Era: "2000s"))
                })
            },

            new List<RouletteQuestion>
            {
                new("origin_q1", "origin", "Pick a travel destination:", new()
                {
                    new("opt_1", "🌸 Neon streets of Tokyo", new(Language: "ja")),
                    new("opt_2", "🦅 Sunny Los Angeles", new(Language: "en")),
                    new("opt_3", "☕ Cozy Parisian cafe", new(Language: "fr")),
                    new("opt_4", "🍜 Late night Seoul", new(Language: "ko")),
                    new("opt_5", "🍕 Coastal town in Italy", new(Language: "it")),
                    new("opt_6", "🌮 Deserts of Mexico", new(Language: "es"))
                }),
                new("origin_q2", "origin", "What's for dinner?", new()
                {
                    new("opt_1", "🍔 A massive cheeseburger", new(Language: "en")),
                    new("opt_2", "🍣 Fresh sashimi & ramen", new(Language: "ja")),
                    new("opt_3", "🍝 Pasta with extra cheese", new(Language: "it")),
                    new("opt_4", "🥐 Croissant & black coffee", new(Language: "fr")),
                    new("opt_5", "🌶️ Spicy kimchi stew", new(Language: "ko"))
                })
            },

            new List<RouletteQuestion>
            {
                new("runtime_q1", "runtime", "How much time do we have?", new()
                {
                    new("opt_1", "⚡ Make it quick (Under 1h 45m)", new(MaxRuntime: 105)),
                    new("opt_2", "🍿 Standard movie night (~2 hours)", new(MaxRuntime: 140)),
                    new("opt_3", "🎬 I want an epic journey (2.5+ hours)", new(MaxRuntime: 200)),
                    new("opt_4", "🕰️ Time is an illusion (I don't care)", new(MaxRuntime: 400))
                }),
                new("runtime_q2", "runtime", "Pick a mode of transport:", new()
                {
                    new("opt_1", "🎢 A crazy rollercoaster", new(MaxRuntime: 105)),
                    new("opt_2", "🚗 A scenic road trip", new(MaxRuntime: 140)),
                    new("opt_3", "🛳️ A long ocean cruise", new(MaxRuntime: 200))
                })
            }
        };

        public const int MaxRandomPageNumber = 15;
        public const int MaxRetryAttemptsCount = 3;
    }
}