# Embeddronics Backend API

## Overview
ASP.NET Core Web API backend for Embeddronics React frontend with authentication, logging, and comprehensive endpoints.

## Features
- ✅ **JWT Authentication** with OTP verification
- ✅ **Serilog Logging** with file and console output
- ✅ **Role-based Authorization** (Admin & User roles)
- ✅ **CORS Configuration** for React frontend
- ✅ **RESTful API Endpoints** for all entities
- ✅ **Admin Dashboard** with log viewing capabilities

## API Endpoints

### Authentication
- `POST /api/auth/login` - Login with username/password, receive OTP
- `POST /api/auth/verify-otp` - Verify OTP and get JWT token

### Public Endpoints
- `GET /api/products` - Get all products
- `GET /api/products/{id}` - Get product by ID
- `GET /api/services` - Get all services
- `GET /api/projects` - Get all projects (portfolio)
- `GET /api/blog` - Get all blog posts
- `GET /api/blog/{id}` - Get blog post by ID (increments views)
- `POST /api/contact` - Submit contact form

### Admin Endpoints (Requires Admin Role)
**Dashboard**
- `GET /api/admin/dashboard` - Get dashboard statistics

**Orders Management**
- `GET /api/admin/orders` - Get all orders
- `GET /api/admin/orders/{id}` - Get order by ID
- `PUT /api/admin/orders/{id}` - Update order

**Clients Management**
- `GET /api/admin/clients` - Get all clients
- `GET /api/admin/clients/{id}` - Get client by ID
- `POST /api/admin/clients` - Create new client
- `PUT /api/admin/clients/{id}` - Update client
- `DELETE /api/admin/clients/{id}` - Delete client

**Leads Management**
- `GET /api/admin/leads` - Get all leads
- `PUT /api/admin/leads/{id}` - Update lead status

**Reviews Management**
- `GET /api/admin/reviews` - Get all reviews
- `PUT /api/admin/reviews/{id}/approve` - Approve review

**Financial Records**
- `GET /api/admin/financial` - Get all financial records
- `POST /api/admin/financial` - Create financial record

**Quotes Management**
- `GET /api/admin/quotes` - Get all quotes
- `POST /api/admin/quotes` - Generate new quote

**Logs Management (Serilog Dashboard)**
- `GET /api/logs/files` - Get list of log files
- `GET /api/logs/view/{fileName}` - View log file content
- `GET /api/logs/search?query={term}` - Search across logs
- `GET /api/logs/stats` - Get logging statistics
- `DELETE /api/logs/{fileName}` - Delete log file

### Content Management (Admin)
- `POST /api/products` - Create product
- `PUT /api/products/{id}` - Update product
- `DELETE /api/products/{id}` - Delete product
- `POST /api/services` - Create service
- `PUT /api/services/{id}` - Update service
- `DELETE /api/services/{id}` - Delete service
- `POST /api/projects` - Create project
- `PUT /api/projects/{id}` - Update project
- `DELETE /api/projects/{id}` - Delete project
- `POST /api/blog` - Create blog post
- `PUT /api/blog/{id}` - Update blog post
- `DELETE /api/blog/{id}` - Delete blog post

## Authentication Flow
1. **Login**: Send username/password to `/api/auth/login`
2. **OTP Generation**: Server generates 6-digit OTP (logged to console/file)
3. **OTP Verification**: Send OTP to `/api/auth/verify-otp`
4. **JWT Token**: Receive JWT token with role information
5. **Authenticated Requests**: Include token in `Authorization: Bearer {token}` header

## Default Credentials
- **Username**: `admin`
- **Password**: Provided via `ADMIN_DEFAULT_PASSWORD` environment variable (or generated in Development). Do NOT commit default passwords to source control.
- **Role**: admin

Note: For production, set `JWT_SECRET` (min 32 chars) and `ADMIN_DEFAULT_PASSWORD` before starting the application.

## Logging
Serilog is configured to log to:
- **Console**: Real-time logs in terminal
- **File**: `logs/embeddronics-log.txt` (daily rolling)
- **Seq** (Optional): http://localhost:5341 for dashboard viewing

All user actions are logged with structured logging for monitoring and troubleshooting.

## Models

### Product
- Id, Name, Description, Price, Category, ImageUrl, InStock, CreatedAt, UpdatedAt

### Service
- Id, Name, Description, Icon, BasePrice, IsActive, CreatedAt

### Project
- Id, Title, Description, Category, ImageUrl, Technologies[], ClientName, CompletedDate, Status

### BlogPost
- Id, Title, Content, Excerpt, Author, ImageUrl, Tags[], PublishedDate, IsPublished, Views

### Order
- Id, OrderNumber, ClientId, ClientName, ClientEmail, TotalAmount, Status, OrderDate, Items[]

### Client
- Id, Name, Email, Phone, Company, Address, RegisteredDate, Status, TotalOrders, TotalSpent

### Lead
- Id, Name, Email, Phone, Company, Message, Source, Status, CreatedDate

### Review
- Id, ClientName, ClientEmail, ProductId, ProductName, Rating, Comment, IsApproved

### FinancialRecord
- Id, Type, Category, Amount, Description, TransactionDate, Reference, PaymentMethod

### Quote
- Id, QuoteNumber, ClientName, ClientEmail, Items[], SubTotal, Tax, Total, Status

## Running the Application

Before starting in Production, make sure to set the following environment variables:

- `JWT_SECRET` (required in Production, minimum 32 characters)
- `ADMIN_DEFAULT_PASSWORD` (optional; used when seeding admin accounts)
- `ADMIN_ROTATE_PASSWORD` (optional; if set, will rotate all admin passwords to this value on startup)

```bash
cd EmbeddronicsBackend/EmbeddronicsBackend
# For development you can set ADMIN_DEFAULT_PASSWORD or let the app generate a password
dotnet run
```

API will be available at: `https://localhost:7XXX` or `http://localhost:5XXX`

## CORS Configuration
React frontend is allowed from:
- http://localhost:5173 (Vite)
- http://localhost:3000 (Create React App)

## Integration with React Frontend

### Example API Calls

**Login Flow:**
```javascript
// 1. Login
const loginResponse = await fetch('http://localhost:5XXX/api/auth/login', {
  method: 'POST',
  headers: { 'Content-Type': 'application/json' },
  body: JSON.stringify({ username: 'admin', password: process.env.ADMIN_DEFAULT_PASSWORD || '<your-admin-password>' })
});

// 2. Verify OTP (check console for OTP)
const otpResponse = await fetch('http://localhost:5XXX/api/auth/verify-otp', {
  method: 'POST',
  headers: { 'Content-Type': 'application/json' },
  body: JSON.stringify({ username: 'admin', otp: '123456' })
});
const { token, role } = await otpResponse.json();

// 3. Use token for authenticated requests
const products = await fetch('http://localhost:5XXX/api/products', {
  headers: { 'Authorization': `Bearer ${token}` }
});
```

**Fetch Products:**
```javascript
const response = await fetch('http://localhost:5XXX/api/products');
const products = await response.json();
```

**Admin Dashboard:**
```javascript
const response = await fetch('http://localhost:5XXX/api/admin/dashboard', {
  headers: { 'Authorization': `Bearer ${adminToken}` }
});
const stats = await response.json();
```

**View Logs (Admin):**
```javascript
const response = await fetch('http://localhost:5XXX/api/logs/files', {
  headers: { 'Authorization': `Bearer ${adminToken}` }
});
const { files } = await response.json();
```

## Next Steps
1. Replace in-memory data service with Entity Framework Core & SQL Server/PostgreSQL
2. Implement email/SMS service for OTP delivery
3. Add file upload for images
4. Set up Seq dashboard for better log visualization
5. Add rate limiting and request validation
6. Implement refresh tokens for JWT
7. Add unit and integration tests
