# EchoColony - AI-Powered Colonist Conversations for RimWorld

🗨️ **Transform your RimWorld colonists into dynamic, AI-powered characters with realistic conversations and personality-driven responses.**

## 🎮 Features

- **Multiple AI Providers**: Choose from Player2, Gemini, Local Models, and OpenRouter
- **Dynamic Conversations**: Context-aware responses based on colonist mood, traits, and relationships  
- **Real-time Interaction**: Chat directly with your colonists during gameplay
- **Memory System**: Colonists remember previous conversations and relationships
- **Personality-Driven**: Responses reflect individual colonist traits and backstories
- **Easy Configuration**: Simple setup with multiple AI service options

## 🚀 Installation

1. Subscribe to the mod on [Steam Workshop](https://steamcommunity.com/sharedfiles/filedetails/?id=3463505750)
2. Choose your preferred AI provider in mod settings
3. Configure your chosen provider (see setup guide below)
4. Start chatting with your colonists!

## 🔧 AI Provider Setup

### 🎙️ Player2 (Recommended - Free & Easy Setup)
Player2 is a free desktop app that provides both AI text and voice without needing API keys or technical setup.

1. **Download & Install**: Get Player2 from [https://player2.game/](https://player2.game/)
2. **Log In**: Create a free account and log into the Player2 app
3. **Auto-Detection**: EchoColony will automatically detect Player2 running in the background
4. **Play**: No additional configuration needed! You can even enable it mid-game

**Benefits**: Your pawns will remember conversations they've already had, providing continuity across gaming sessions.

### 🧠 Google Gemini
1. Get free API key from [Google AI Studio](https://aistudio.google.com/app/apikey)
2. Select "Gemini" in mod settings
3. Enter your API key

### 🏠 Local Models (Ollama, LMStudio, KoboldAI)
1. Set up your local AI server
2. Select "Local" provider
3. Configure endpoint URL and model name

### 🌐 OpenRouter
1. Get API key from [OpenRouter](https://openrouter.ai/)
2. Select "OpenRouter" provider  
3. Choose your preferred model

## 📋 Requirements

- **RimWorld 1.5+**
- **Harmony** (auto-installed)
- **Internet connection** (for cloud AI providers) or **Player2 app** (for Player2 provider)

## 🎛️ Configuration Options

- **Model Selection**: Choose between different AI models for various response styles
- **Response Length**: Adjust conversation depth and detail
- **Personality Settings**: Fine-tune how traits affect responses
- **Memory Duration**: Control how long colonists remember conversations
- **Debug Mode**: Enable detailed logging for troubleshooting

## 🔧 Development Status

This project represents an early exploration into AI-powered RimWorld modding. As my first venture into mod development, the codebase contains:

- **Mixed documentation** in both English and Spanish
- **Experimental implementations** that evolved during development
- **Learning-oriented architecture** that prioritized functionality over optimization

While the mod is fully functional and tested, contributors should expect to encounter varied coding styles and documentation approaches. This authentic development history is preserved to show the iterative nature of modding and learning.

**Future improvements welcome** - including code refactoring, documentation standardization, and architectural enhancements.

## 🤝 Contributing & Community

### ✅ We Welcome:
- Code cleanup and refactoring
- Documentation improvements and standardization
- Bug reports and fixes
- Feature suggestions and implementations
- Translation contributions
- UI/UX improvements
- New AI provider integrations (alongside existing ones)
- Performance optimizations

### 📁 Project Structure:
```
EchoColony/
├── Source/              # C# source code (mixed EN/ES documentation)
├── About/              # Mod metadata
├── Defs/               # XML game definitions  
├── Assemblies/         # Compiled DLLs
└── Languages/          # Localization files
```

### 🚀 How to Contribute:
1. Fork this repository
2. Create a feature branch: `git checkout -b feature/amazing-feature`
3. Make your changes (see requirements below)
4. Test thoroughly with multiple AI providers
5. Submit a Pull Request

## ⚖️ License & Requirements

This project is licensed under a **Modified MIT License** with specific requirements for derivative works.

### 🔒 MANDATORY Requirements for ALL Forks/Modifications:

If you create **any** derivative work or modification, you **MUST**:

#### 🎯 Player2 Integration (NON-NEGOTIABLE):
- **Preserve Player2** as an available AI provider option
- **Use Official Game Client ID**: `019a8368-b00b-72bc-b367-2825079dc6fb`
- **Maintain Health Checks**: Automated 60-second health monitoring when Player2 is active
- **Keep Authentication**: Preserve Player2's authentication methods
- **No Removal**: Cannot disable, remove, or break Player2 functionality

#### 🏷️ Attribution Requirements:
- **Credit Original Author**: Prominent attribution to CarlosNahuelcoy
- **Preserve License**: Include this license in all distributions
- **No False Claims**: Cannot claim original authorship of Player2 components

#### 💻 Technical Requirements:
```csharp
// Required implementation elements:
private const string GameClientId = "019a8368-b00b-72bc-b367-2825079dc6fb";
// Health checks every 60 seconds when Player2 is active
// Player2 must remain in provider options
```

### ✅ What You CAN Do:
- ✅ Add new AI providers alongside Player2
- ✅ Improve UI/UX while maintaining Player2 access
- ✅ Fix bugs and optimize performance  
- ✅ Enhance features while preserving Player2
- ✅ Create translations and documentation
- ✅ Refactor and clean up code
- ✅ Standardize documentation language

### ❌ What You CANNOT Do:
- ❌ Remove Player2 as an option
- ❌ Change the Game Client ID
- ❌ Disable health check functionality
- ❌ Break Player2 authentication
- ❌ Claim original authorship
- ❌ Create "Player2-free" versions

**Violation of these requirements terminates all license rights.**

## 🐛 Bug Reports

When reporting issues, please include:

- **AI Provider**: Which service you were using (Player2, Gemini, etc.)
- **RimWorld Version**: Your game version
- **Mod Version**: EchoColony version
- **Error Details**: Specific error messages or unexpected behavior
- **Steps to Reproduce**: How to recreate the issue

**For Player2 Issues**, also include:
- Player2 app version and status
- Health check status in debug logs
- Game Client ID verification

## 🌟 Why Player2?

Player2 offers several advantages for character conversations:
- **Zero Setup**: No API keys or technical configuration required
- **Free to Use**: No usage limits or costs
- **Voice Integration**: Text-to-speech capabilities
- **Automatic Detection**: EchoColony finds it automatically
- **Memory Continuity**: Conversations persist across game sessions
- **Background Operation**: Runs seamlessly while you play

## 🙏 Credits

- **Original Author**: CarlosNahuelcoy (Gerik Uylerk)
- **Inspired by**: The RimWorld modding community's innovation
- **Special Thanks**: Player2 team for accessible AI integration

## 📄 Legal

This project is licensed under the [Modified MIT License](LICENSE) with specific requirements for Player2 integration preservation. See LICENSE file for complete terms.

**Not affiliated with Ludeon Studios or Player2.ai**

---

**Transform your colony's story with AI-powered personalities! 🚀**
