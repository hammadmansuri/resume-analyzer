# GitHub & Render Deployment Guide

This guide walks through creating a GitHub repository and deploying Resume Analyzer to Render.

## Prerequisites

- GitHub account ([create one](https://github.com/signup))
- Render account ([create one](https://render.com))
- OpenAI API key ([get it here](https://platform.openai.com/api-keys))
- Git installed locally

## Step 1: Create GitHub Repository

### Option A: Using GitHub Web UI (Easiest)

1. Go to [github.com/new](https://github.com/new)
2. Fill in:
   - **Repository name**: `resume-analyzer`
   - **Description**: "AI-powered resume analysis tool"
   - **Visibility**: Public (so Render can access it)
3. Click **Create repository**
4. You'll see quick setup instructions—copy the commands

### Option B: Using Git CLI

```bash
cd c:\Users\hamma\projects\resume-analyzer

# Initialize local git repo
git init

# Add all files
git add .

# Commit
git commit -m "Initial commit: Resume Analyzer with Razor UI"

# Add remote (replace YOUR_USERNAME with your GitHub username)
git remote add origin https://github.com/hammadmansuri/resume-analyzer.git

# Rename branch to main
git branch -M main

# Push to GitHub
git push -u origin main
```

## Step 2: Configure Git User (First Time Only)

If this is your first time using Git, configure your identity:

```bash
git config --global user.name "Your Name"
git config --global user.email "your.email@example.com"
```

## Step 3: Verify Repository on GitHub

1. Go to `https://github.com/hammadmansuri/resume-analyzer`
2. You should see:
   - All your project files
   - README.md displayed below
   - Dockerfile visible

## Step 4: Deploy to Render

### Step 1: Connect GitHub to Render

1. Go to [render.com](https://render.com)
2. Sign up or log in
3. Click **Dashboard** → **New +** → **Web Service**
4. Select **GitHub** as deployment source
5. Authorize Render to access your GitHub account
6. Select your `resume-analyzer` repository

### Step 2: Configure Render Service

Render will auto-detect most settings, but verify:

- **Name**: `resume-analyzer-api` ✓
- **Runtime**: `Docker` ✓
- **Dockerfile**: `./Dockerfile` ✓
- **Branch**: `main` ✓
- **Plan**: Free tier is fine for testing

### Step 3: Add Environment Variables

1. Scroll down to **Environment**
2. Click **Add Environment Variable**
3. Add:
   - **Key**: `OPENAI_API_KEY`
   - **Value**: (paste your OpenAI API key)
   - **Check "Sync to Preview Deployments"** (optional)

4. Click **Save**

### Step 4: Deploy

1. Click **Create Web Service** at the bottom
2. Render will start building:
   - Pulling from GitHub
   - Building Docker image
   - Deploying to their servers

3. Monitor progress in the **Logs** tab
4. When complete, you'll see a green checkmark ✓
5. Your service URL appears at the top (e.g., `https://resume-analyzer-api.onrender.com`)

## Verify Deployment

1. Visit your Render URL in a browser
2. You should see the Resume Analyzer form
3. Test with:
   - **Target Role**: ".NET Developer"
   - **Resume Text**: "5 years C# experience, ASP.NET Core, Docker"

## Update Deployment

After making code changes:

```bash
# Make your changes locally
# Then:

git add .
git commit -m "Description of changes"
git push origin main

# Render automatically redeploys!
```

## Troubleshooting

### "Build Failed"

Check the **Logs** tab in Render. Common issues:
- **Missing OpenAI API key**: Add to Environment variables
- **Dockerfile error**: Ensure it's in the root directory

### "Application Won't Start"

1. Check **Logs** for error messages
2. Verify `OPENAI_API_KEY` is set
3. Click **Manual Deploy** to retry

### "Stuck on Building"

Render free tier sometimes queues builds. Wait 5-10 minutes, then check logs.

### "Not responding"

The free tier may auto-pause after inactivity. When you visit the URL, it will wake up (may take 30 seconds).

## SSL Certificate (HTTPS)

Render automatically provides HTTPS for your service. Your URL will be:
```
https://resume-analyzer-api.onrender.com
```

## Local Testing Before Deploy

To test locally with the same setup as Render:

```bash
# Build Docker image locally
docker build -t resume-analyzer .

# Run with OpenAI key
docker run -e OPENAI_API_KEY="your-key" -p 5000:5000 resume-analyzer

# Visit http://localhost:5000
```

## Monitoring & Logs

**View logs in Render:**
1. Go to your service page on Render
2. Click **Logs** tab
3. Scroll to see real-time requests and errors

**Monitor performance:**
- Render provides memory/CPU usage graphs
- See request count and response times

## Custom Domain (Optional)

To use a custom domain like `resume-analyzer.com`:

1. In Render dashboard, go to your service
2. Click **Settings** → **Custom Domain**
3. Add your domain
4. Update DNS records at your domain registrar
5. Render will verify and provision SSL

## Cost

- **Free tier**: $0/month
  - Free database & web services
  - Automatic sleep after 15 min inactivity
  - Good for demos & testing

- **Paid tier**: Starting $7/month
  - Always running
  - Better performance
  - No auto-sleep

## Next Steps

1. ✅ Create GitHub repo
2. ✅ Deploy to Render
3. 📊 Share your Resume Analyzer link
4. 🚀 Iterate based on feedback

## Support

- **GitHub Issues**: [Report bugs](https://github.com/hammadmansuri/resume-analyzer/issues)
- **Render Docs**: [Render Documentation](https://render.com/docs)
- **OpenAI Docs**: [OpenAI API Reference](https://platform.openai.com/docs)

---

**Your Resume Analyzer is now live on the internet!** 🎉
