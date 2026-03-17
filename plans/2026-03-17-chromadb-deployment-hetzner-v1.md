# Deployment Plan: ChromaDB Integration to Hetzner Server

## Objective

Deploy the ChromaDB integration changes to the production Hetzner server, including:
- Pushing C# changes to the bot repository
- Installing and configuring ChromaDB on the server
- Updating the PHP booking files with ChromaDB integration

---

## Implementation Plan

- [ ] Task 1. Commit and push all local changes to git
- [ ] Task 2. SSH to Hetzner server
- [ ] Task 3. Navigate to bot directory and pull latest changes
- [ ] Task 4. Install and deploy ChromaDB on the server
- [ ] Task 5. Configure ChromaDB in appsettings.json
- [ ] Task 6. Restart the .NET solution with ChromaDB
- [ ] Task 7. Copy PHP files to /var/www/alqueriavillacarmen
- [ ] Task 8. Verify ChromaDB is accessible from bot
- [ ] Task 9. Test the integration end-to-end

## Verification Criteria

- [ ] Git push successful
- [ ] ChromaDB running on server
- [ ] Bot can connect to ChromaDB
- [ ] PHP files updated on server
- [ ] No errors in bot logs

## Potential Risks and Mitigations

1. **Git conflicts on server**
   Mitigation: Use `git reset --hard origin/main` to force overwrite

2. **ChromaDB container fails to start**
   Mitigation: Check Docker is installed, check port 8000 is not in use

3. **Bot fails to start after changes**
   Mitigation: Check appsettings.json configuration, check ChromaDB connectivity

4. **PHP files not writable**
   Mitigation: Check file permissions, use sudo if needed

## Alternative Approaches

1. **Manual deployment**: Instead of git pull, manually upload files via SCP
2. **Docker Compose for all services**: Use docker-compose to manage both bot and ChromaDB
3. **Existing ChromaDB**: If ChromaDB already exists on server, just update configuration
