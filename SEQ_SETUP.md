# Setting Up Serilog Dashboard with Seq

## What is Seq?
Seq is a centralized log server and dashboard for viewing, searching, and analyzing structured logs from Serilog.

## Installation

### Option 1: Docker (Recommended)
```bash
docker run --name seq -d --restart unless-stopped -e ACCEPT_EULA=Y -p 5341:80 datalust/seq:latest
```

### Option 2: Windows Install
1. Download from: https://datalust.co/download
2. Run the installer
3. Seq will be available at http://localhost:5341

### Option 3: Skip Seq (Optional)
If you don't want to use Seq, remove the Seq sink from Program.cs:
```csharp
// Remove this line:
.WriteTo.Seq("http://localhost:5341")
```

## Accessing Seq Dashboard
1. Open your browser
2. Navigate to: http://localhost:5341
3. You'll see all logs from your Embeddronics backend

## Features in Seq Dashboard
- **Real-time Logs**: See logs as they happen
- **Search & Filter**: Search by log level, message, user, etc.
- **Structured Data**: Click on any log to see full details
- **Queries**: Use SQL-like queries to filter logs
- **Dashboards**: Create custom dashboards for monitoring
- **Alerts**: Set up alerts for errors or specific events

## Example Queries in Seq

### View all login attempts
```
@Message like '%Login attempt%'
```

### View failed operations
```
@Level = 'Warning' or @Level = 'Error'
```

### View logs for specific user
```
User = 'admin'
```

### View logs from last hour
```
@Timestamp > Now()-1h
```

## Alternative: Using Built-in Logs API
The backend also includes a REST API for viewing logs without Seq:

**Get log files:**
```
GET /api/logs/files
Authorization: Bearer {admin-token}
```

**View log file:**
```
GET /api/logs/view/{fileName}
Authorization: Bearer {admin-token}
```

**Search logs:**
```
GET /api/logs/search?query=error
Authorization: Bearer {admin-token}
```

**Get statistics:**
```
GET /api/logs/stats
Authorization: Bearer {admin-token}
```

This allows you to build a custom log viewer in your React admin panel!

## Log File Location
Logs are also written to: `EmbeddronicsBackend/logs/embeddronics-log.txt`

Each day creates a new file: `embeddronics-log20231223.txt`
