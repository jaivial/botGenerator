#!/bin/bash

# Deploy script for BotGenerator WhatsApp Bot
# Commits and pushes local changes, then connects to server, pulls latest code, builds and restarts service

echo "🚀 Starting BotGenerator deployment..."

# Check if there are any changes to commit
if [ -n "$(git status --porcelain)" ]; then
    echo "📝 Committing local changes..."

    # Get current timestamp for commit message
    TIMESTAMP=$(date '+%Y-%m-%d %H:%M:%S')

    # Add all changes
    git add .

    # Create commit with timestamp
    git commit -m "Auto-deployment commit - $TIMESTAMP

🤖 Generated with [Claude Code](https://claude.ai/code)

Co-Authored-By: Claude <noreply@anthropic.com>"

    if [ $? -eq 0 ]; then
        echo "✅ Changes committed successfully"
    else
        echo "❌ Failed to commit changes"
        exit 1
    fi
else
    echo "ℹ️  No local changes to commit"
fi

# Push to origin
echo "📤 Pushing to remote repository..."
git push origin master

if [ $? -eq 0 ]; then
    echo "✅ Pushed to remote successfully"
else
    echo "❌ Failed to push to remote"
    exit 1
fi

echo "🔗 Connecting to server and deploying..."

ssh root@178.16.130.178 << 'EOF'
cd /var/www/alqueriavillacarmen.com/bot
echo "📁 Current directory: $(pwd)"

echo "🔄 Fetching latest changes from remote..."
git fetch origin
git reset --hard origin/master

echo "⏹️  Stopping botgenerator service for build..."
systemctl stop botgenerator

echo "🔨 Building application..."
dotnet publish src/BotGenerator.Api/BotGenerator.Api.csproj -c Release -o ./publish

if [ $? -eq 0 ]; then
    echo "✅ Build successful"
else
    echo "❌ Build failed"
    exit 1
fi

echo "🔄 Restarting botgenerator service..."
systemctl restart botgenerator

sleep 2

echo "📊 Service status:"
systemctl status botgenerator --no-pager -l | head -20

echo ""
echo "🎉 Deployment completed!"
EOF

echo "✅ Deploy script finished."
