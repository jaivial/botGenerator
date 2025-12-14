# BotGenerator VPS Deployment Guide

## Server Information
- **Server:** 178.16.130.178
- **OS:** Ubuntu 24.04.2 LTS
- **Domain:** alqueriavillacarmen.com
- **Bot Port:** 5050 (free, recommended)

## Used Ports (avoid these)
```
80, 443        - nginx
3000-3003      - node/next.js apps
4321, 4322     - node apps
5432, 5434     - postgres
5678           - docker
6379           - redis
8000, 8080     - python
8081-8102      - various services
9000, 9876     - python
27017          - mongodb
3306, 33060    - mysql
```

---

## Step 1: Install .NET 8 Runtime

SSH into your server:
```bash
ssh root@178.16.130.178
```

Install .NET 8 SDK/Runtime:
```bash
# Add Microsoft package repository
wget https://packages.microsoft.com/config/ubuntu/24.04/packages-microsoft-prod.deb -O packages-microsoft-prod.deb
dpkg -i packages-microsoft-prod.deb
rm packages-microsoft-prod.deb

# Install .NET 8 Runtime (for production) and SDK (for building)
apt update
apt install -y dotnet-sdk-8.0

# Verify installation
dotnet --version
```

---

## Step 2: Create Application Directory

```bash
# Create directory for the bot
mkdir -p /var/www/alqueriavillacarmen.com/bot
cd /var/www/alqueriavillacarmen.com/bot
```

---

## Step 3: Deploy the Application

### Option A: Build locally and copy files

On your local machine:
```bash
cd /home/jaime/Documents/projects/botGenerator
dotnet publish src/BotGenerator.Api/BotGenerator.Api.csproj -c Release -o ./publish
```

Copy to server:
```bash
scp -r ./publish/* root@178.16.130.178:/var/www/alqueriavillacarmen.com/bot/
```

### Option B: Clone and build on server

```bash
cd /var/www/alqueriavillacarmen.com/bot
git clone https://github.com/jaivial/botGenerator.git .
dotnet publish src/BotGenerator.Api/BotGenerator.Api.csproj -c Release -o ./publish
```

---

## Step 4: Configure Environment Variables

Create/update the .env file:
```bash
nano /var/www/alqueriavillacarmen.com/.env
```

Add these variables (merge with existing):
```env
# ============ BOT GENERATOR CONFIG ============
# Google AI / Gemini
GOOGLE_AI_API_KEY=your_gemini_api_key_here

# WhatsApp via UAZAPI
UAZAPI_URL=https://alqueriavillacarmen.uazapi.com
UAZAPI_TOKEN=0a8ea30d-8717-4c64-acd6-95b78f840210

# MySQL (use existing HOSTINGER config)
DB_HOST_HOSTINGER=127.0.0.1
DB_USER_HOSTINGER=villacarmen_user
DB_PASSWORD_HOSTINGER=Jva-Mvc-5171
DB_NAME_HOSTINGER=villacarmen_local

# Redis (optional, for session state)
REDIS_CONNECTION_STRING=localhost:6379

# ASP.NET Core settings
ASPNETCORE_ENVIRONMENT=Production
ASPNETCORE_URLS=http://localhost:5050
```

---

## Step 5: Create Systemd Service

Create a service file:
```bash
nano /etc/systemd/system/botgenerator.service
```

Add this content:
```ini
[Unit]
Description=BotGenerator WhatsApp Bot API
After=network.target mysql.service

[Service]
WorkingDirectory=/var/www/alqueriavillacarmen.com/bot/publish
ExecStart=/usr/bin/dotnet /var/www/alqueriavillacarmen.com/bot/publish/BotGenerator.Api.dll
Restart=always
RestartSec=10
KillSignal=SIGINT
SyslogIdentifier=botgenerator
User=www-data
Group=www-data

# Environment variables
EnvironmentFile=/var/www/alqueriavillacarmen.com/.env
Environment=ASPNETCORE_ENVIRONMENT=Production
Environment=ASPNETCORE_URLS=http://localhost:5050
Environment=DOTNET_PRINT_TELEMETRY_MESSAGE=false

[Install]
WantedBy=multi-user.target
```

Enable and start the service:
```bash
# Set correct permissions
chown -R www-data:www-data /var/www/alqueriavillacarmen.com/bot

# Reload systemd
systemctl daemon-reload

# Enable service to start on boot
systemctl enable botgenerator

# Start the service
systemctl start botgenerator

# Check status
systemctl status botgenerator

# View logs
journalctl -u botgenerator -f
```

---

## Step 6: Configure Nginx Reverse Proxy

Edit nginx config:
```bash
nano /etc/nginx/sites-available/alqueriavillacarmen.com
```

Add this location block inside the `server` block (after the `/evolution/` location):
```nginx
    # BotGenerator API
    location /api/bot/ {
        proxy_pass http://localhost:5050/;
        proxy_http_version 1.1;
        proxy_set_header Upgrade $http_upgrade;
        proxy_set_header Connection "upgrade";
        proxy_set_header Host $host;
        proxy_set_header X-Real-IP $remote_addr;
        proxy_set_header X-Forwarded-For $proxy_add_x_forwarded_for;
        proxy_set_header X-Forwarded-Proto $scheme;
        proxy_read_timeout 300s;
        proxy_connect_timeout 75s;
    }
```

Test and reload nginx:
```bash
nginx -t
systemctl reload nginx
```

---

## Step 7: Configure UAZAPI Webhook

Your webhook URL will be:
```
https://alqueriavillacarmen.com/api/bot/whatsapp-webhook
```

### Configure in UAZAPI Dashboard:

1. Go to your UAZAPI dashboard: https://alqueriavillacarmen.uazapi.com
2. Navigate to **Webhook Settings** or **Instance Configuration**
3. Set the webhook URL to:
   ```
   https://alqueriavillacarmen.com/api/bot/whatsapp-webhook
   ```
4. Enable these webhook events:
   - `messages` (incoming messages)
   - `messages.update` (message status updates)
   - `connection.update` (connection status)

### Test the webhook:
```bash
# Test from your local machine
curl -X POST https://alqueriavillacarmen.com/api/bot/whatsapp-webhook \
  -H "Content-Type: application/json" \
  -d '{
    "event": "messages",
    "message": {
      "chatid": "34612345678@s.whatsapp.net",
      "text": "Test",
      "messageType": "text",
      "fromMe": false
    },
    "chat": {"name": "Test"}
  }'

# Should return {"processed":true}
```

---

## Step 8: Verify Deployment

### Check service status:
```bash
systemctl status botgenerator
```

### Check logs:
```bash
journalctl -u botgenerator -f --no-pager -n 50
```

### Test health endpoint:
```bash
curl https://alqueriavillacarmen.com/api/bot/health
# Returns: {"status":"healthy","timestamp":"...","version":"1.0.0"}
```

### Test webhook manually:
```bash
curl -X POST https://alqueriavillacarmen.com/api/bot/whatsapp-webhook \
  -H "Content-Type: application/json" \
  -d '{
    "event": "messages",
    "message": {
      "chatid": "34612345678@s.whatsapp.net",
      "text": "Hola, quiero reservar",
      "messageType": "text",
      "fromMe": false
    },
    "chat": {"name": "Test User"}
  }'
# Returns: {"processed":true}
```

---

## Troubleshooting

### View real-time logs:
```bash
journalctl -u botgenerator -f
```

### Restart the service:
```bash
systemctl restart botgenerator
```

### Check if port is listening:
```bash
netstat -tlnp | grep 5050
```

### Check nginx error logs:
```bash
tail -f /var/log/nginx/alqueriavillacarmen.error.log
```

### Test MySQL connection:
```bash
mysql -u villacarmen_user -p'Jva-Mvc-5171' -h 127.0.0.1 villacarmen_local -e "SELECT 1"
```

---

## Quick Commands Reference

```bash
# Start/Stop/Restart
systemctl start botgenerator
systemctl stop botgenerator
systemctl restart botgenerator

# View status
systemctl status botgenerator

# View logs
journalctl -u botgenerator -f

# Redeploy
cd /var/www/alqueriavillacarmen.com/bot
git pull
dotnet publish src/BotGenerator.Api/BotGenerator.Api.csproj -c Release -o ./publish
systemctl restart botgenerator
```

---

## Environment Variables Reference

| Variable | Description | Example |
|----------|-------------|---------|
| `GOOGLE_AI_API_KEY` | Gemini API key | `AIza...` |
| `UAZAPI_URL` | UAZAPI base URL | `https://alqueriavillacarmen.uazapi.com` |
| `UAZAPI_TOKEN` | UAZAPI authentication token | `0a8ea30d-...` |
| `DB_HOST_HOSTINGER` | MySQL host | `127.0.0.1` |
| `DB_USER_HOSTINGER` | MySQL username | `villacarmen_user` |
| `DB_PASSWORD_HOSTINGER` | MySQL password | `***` |
| `DB_NAME_HOSTINGER` | MySQL database name | `villacarmen_local` |
| `REDIS_CONNECTION_STRING` | Redis connection (optional) | `localhost:6379` |
| `ASPNETCORE_ENVIRONMENT` | Environment mode | `Production` |
| `ASPNETCORE_URLS` | Listening URL | `http://localhost:5050` |
