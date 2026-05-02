#!/bin/bash
# Quick deployment script for Resume Analyzer
# Usage: ./deploy.sh

set -e

echo "🚀 Resume Analyzer - Quick Deploy"
echo "=================================="
echo ""

# Check if git is initialized
if [ ! -d .git ]; then
    echo "📦 Initializing Git repository..."
    git init
    git config user.name "Resume Analyzer"
    git config user.email "dev@example.com"
else
    echo "✅ Git repository already initialized"
fi

echo ""
echo "📝 Staging all files..."
git add .

echo "📝 Creating commit..."
git commit -m "Deploy: Resume Analyzer with Razor UI" --allow-empty

echo ""
echo "🔗 Setting remote origin (if not already set)..."
git remote add origin https://github.com/hammadmansuri/resume-analyzer.git 2>/dev/null || true

echo ""
echo "🌿 Ensuring main branch..."
git branch -M main

echo ""
echo "📤 Pushing to GitHub..."
echo "NOTE: You may be prompted for GitHub credentials"
echo ""
git push -u origin main

echo ""
echo "✅ Repository pushed to GitHub!"
echo ""
echo "Next steps:"
echo "1. Go to https://github.com/hammadmansuri/resume-analyzer"
echo "2. Verify your files are there"
echo "3. Go to https://render.com and deploy"
echo ""
echo "For detailed deployment guide, see DEPLOYMENT.md"
