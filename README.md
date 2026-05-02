# Resume Analyzer 🚀

An AI-powered resume analysis tool built with ASP.NET Core and Razor Pages. Get realistic, actionable feedback on your readiness for any target role.

## Features

✨ **Intelligent Analysis**
- AI-powered resume evaluation using OpenAI GPT
- Role-specific skill assessment
- Realistic readiness scoring (not inflated)
- Identification of missing skills (must-have vs. good-to-have)

🎯 **Actionable Insights**
- Concrete action items with difficulty and time estimates
- Beginner-friendly starter task
- Role fit assessment with suggestions
- Recognition of existing strengths

🎨 **Beautiful UI**
- Clean, modern Razor Pages interface
- File upload or text paste
- Results dashboard with visual scoring
- Responsive design (mobile-friendly)

🚀 **Production-Ready**
- Error resilience with graceful fallbacks
- Docker containerization
- One-click Render deployment
- REST API endpoint for programmatic use

## Tech Stack

- **Backend**: ASP.NET Core 8.0
- **Frontend**: Razor Pages + HTML/CSS
- **AI**: OpenAI GPT API
- **PDF Support**: UglyToad.PdfPig
- **Deployment**: Docker + Render
- **Version Control**: Git/GitHub

## Quick Start (Local Development)

### Prerequisites

- .NET 8.0 SDK or later
- OpenAI API key (get it at [platform.openai.com](https://platform.openai.com))

### Setup

1. **Clone the repository**
   ```bash
   git clone https://github.com/hammadmansuri/resume-analyzer.git
   cd resume-analyzer
   ```

2. **Set environment variables**
   ```bash
   # On Windows (PowerShell)
   $env:OPENAI_API_KEY = "your-api-key-here"
   
   # On Linux/macOS
   export OPENAI_API_KEY="your-api-key-here"
   ```

3. **Run the application**
   ```bash
   dotnet run
   ```

4. **Open in browser**
   ```
   http://localhost:5000
   ```

## API Usage

### Endpoint: POST /api/analyze-resume

Analyze a resume programmatically via the REST API.

**Request (multipart/form-data):**
```bash
curl -X POST http://localhost:5000/api/analyze-resume \
  -F "targetRole=.NET Developer" \
  -F "resumeFile=@resume.pdf"
```

**Request (JSON):**
```bash
curl -X POST http://localhost:5000/api/analyze-resume \
  -H "Content-Type: application/json" \
  -d '{
    "targetRole": ".NET Developer",
    "resumeText": "John Doe\n5 years experience with C#, ASP.NET Core..."
  }'
```

**Response:**
```json
{
  "score": 74,
  "strengths": [
    "Strong experience in backend development with .NET",
    "Good exposure to microservices architecture"
  ],
  "missingSkills": {
    "mustHave": [
      "Docker",
      "API security (JWT/OAuth2)"
    ],
    "goodToHave": [
      "CI/CD pipelines",
      "System design basics"
    ]
  },
  "actions": [
    {
      "task": "Dockerize an existing ASP.NET Core Web API and run it locally",
      "difficulty": "easy",
      "time": "2 days"
    },
    {
      "task": "Implement JWT authentication in a sample API project",
      "difficulty": "medium",
      "time": "3 days"
    }
  ],
  "roleAssessment": {
    "fit": "well-matched",
    "suggestedRole": ".NET Developer",
    "reason": "Candidate has good backend fundamentals with room for growth."
  },
  "firstStep": "Pick one existing project and containerize it using Docker today"
}
```

## Deployment to Render

### Option 1: Connect GitHub (Recommended)

1. **Create GitHub repository**
   ```bash
   # Initialize git
   git init
   git add .
   git commit -m "Initial commit: Resume Analyzer"
   
   # Create repository on GitHub
   # Then push:
   git remote add origin https://github.com/hammadmansuri/resume-analyzer.git
   git branch -M main
   git push -u origin main
   ```

2. **Deploy to Render**
   - Go to [render.com](https://render.com)
   - Sign in with GitHub
   - Click "New +" → "Web Service"
   - Connect your GitHub repository
   - Select this repository and branch
   - Render will auto-detect the Dockerfile
   - Add environment variables:
     - `OPENAI_API_KEY`: Your OpenAI API key
   - Click "Create Web Service"

### Option 2: Deploy with Render CLI

```bash
# Install Render CLI (optional)
npm install -g render-cli

# Deploy using render.yaml
render deploy
```

### Environment Variables Required

Set these in Render dashboard under "Environment":

- `OPENAI_API_KEY`: Your OpenAI API key (required)
- `ASPNETCORE_ENVIRONMENT`: Set to `Production`
- `PORT`: Render sets this automatically (default: 5000)

## Project Structure

```
resume-analyzer/
├── Pages/                      # Razor Pages
│   ├── Shared/
│   │   └── _Layout.cshtml     # Master layout
│   ├── Index.cshtml           # Form page
│   ├── Index.cshtml.cs        # Form logic
│   ├── Results.cshtml         # Results display
│   ├── Results.cshtml.cs      # Results logic
│   ├── _ViewImports.cshtml
│   └── _ViewStart.cshtml
├── Models/                     # Data models
│   ├── ResumeAnalysisRequest.cs
│   ├── ResumeAnalysisResponse.cs
│   └── ...
├── Services/                   # Business logic
│   └── OpenAiClient.cs
├── wwwroot/
│   └── css/
│       └── site.css           # Styling
├── Program.cs                 # Application startup
├── Dockerfile                 # Docker configuration
├── render.yaml                # Render deployment config
└── .gitignore
```

## Configuration

### Role Expectations

The system comes with predefined role expectations for:
- `.NET Developer`
- `Java Developer`
- `Frontend Developer`
- `Full Stack Developer`

For other roles, it dynamically generates expectations via OpenAI. This makes the system flexible for any role.

### Scoring Rules

- **55–75**: Most real candidates fall here
- **75–85**: Strong candidates with targeted growth
- **85+**: Rare, only exceptional candidates

The system avoids inflating scores and focuses on realistic assessments.

## Error Handling

The application includes resilient JSON parsing with fallback responses. If OpenAI returns unexpected JSON structure:
1. Custom converters handle type mismatches (booleans → strings, etc.)
2. Fallback response is returned instead of crashing
3. Errors are logged for debugging
4. User gets helpful error message to retry

## Contributing

Pull requests welcome! Please:
1. Fork the repository
2. Create a feature branch (`git checkout -b feature/amazing-feature`)
3. Commit changes (`git commit -m 'Add amazing feature'`)
4. Push to branch (`git push origin feature/amazing-feature`)
5. Open a Pull Request

## License

MIT License - see LICENSE file for details

## Support

- **Issues**: Report bugs on [GitHub Issues](https://github.com/hammadmansuri/resume-analyzer/issues)
- **Discussions**: Share ideas on [GitHub Discussions](https://github.com/hammadmansuri/resume-analyzer/discussions)

## Future Enhancements

- [ ] User authentication & resume history
- [ ] Resume comparison against job descriptions
- [ ] Salary prediction based on skills
- [ ] Interview preparation guide
- [ ] Export results to PDF
- [ ] API rate limiting & usage analytics

## Acknowledgments

- Built with [OpenAI GPT API](https://openai.com/api/)
- PDF handling by [UglyToad.PdfPig](https://github.com/UglyToad/PdfPig)
- Deployed on [Render](https://render.com)

---

**Made with ❤️ to help developers succeed in their careers.**
