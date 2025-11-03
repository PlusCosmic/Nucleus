# Discord OAuth2 Implementation Guide

## Overview

Discord OAuth2 enables applications to authenticate users and access Discord API resources on their behalf. This guide covers the essential concepts, flows, and implementation details for integrating Discord OAuth2 into your application.

## Table of Contents

1. [Getting Started](#getting-started)
2. [OAuth2 Flows](#oauth2-flows)
3. [OAuth2 Scopes](#oauth2-scopes)
4. [Authorization URL](#authorization-url)
5. [Token Exchange](#token-exchange)
6. [Refreshing Tokens](#refreshing-tokens)
7. [Making API Requests](#making-api-requests)
8. [Security Considerations](#security-considerations)
9. [Code Examples](#code-examples)

---

## Getting Started

### Prerequisites

1. **Create a Discord Application**
   - Go to [Discord Developer Portal](https://discord.com/developers/applications)
   - Click "New Application"
   - Note your **Client ID** and **Client Secret**

2. **Configure OAuth2 Redirect URIs**
   - Navigate to OAuth2 section in your application
   - Add redirect URIs (e.g., `http://localhost:3000/callback`)
   - These must match exactly when making authorization requests

### Key Endpoints

- **Authorization URL**: `https://discord.com/api/oauth2/authorize`
- **Token URL**: `https://discord.com/api/oauth2/token`
- **Token Revocation**: `https://discord.com/api/oauth2/token/revoke`
- **User Info**: `https://discord.com/api/users/@me`

---

## OAuth2 Flows

Discord supports several OAuth2 grant types:

### 1. Authorization Code Grant (Recommended)

**Best for**: Server-side applications that can securely store client secrets.

**Flow**:
1. Redirect user to Discord authorization URL
2. User authorizes application
3. Discord redirects back with authorization code
4. Exchange code for access token and refresh token
5. Use access token to make API requests

**Benefits**:
- Most secure flow
- Provides refresh tokens
- Tokens stored server-side

### 2. Implicit Grant (Deprecated)

**Warning**: Vulnerable to token leakage and replay attacks. Use Authorization Code Grant instead.

**Flow**:
1. Redirect user to Discord with `response_type=token`
2. Access token returned directly in URL fragment
3. No refresh token provided

### 3. Client Credentials Grant

**Best for**: Bot developers testing their own bearer tokens.

**Flow**:
1. Make POST request to token URL with `grant_type=client_credentials`
2. Use Basic authentication (Client ID as username, Client Secret as password)
3. Receive access token for bot owner

---

## OAuth2 Scopes

Scopes define what access your application requests from the user's Discord account.

| Scope | Description | Approval Required |
|-------|-------------|-------------------|
| `activities.read` | Read user's activity status | No |
| `activities.write` | Update user's activity | Approval |
| `applications.builds.read` | Read build data for user's applications | No |
| `applications.builds.upload` | Upload/update builds for user's applications | No |
| `applications.commands` | Create commands in a guild | No |
| `applications.commands.update` | Update commands via bearer token | No |
| `applications.commands.permissions.update` | Update command permissions | No |
| `applications.entitlements` | Read entitlements for user's applications | No |
| `applications.store.update` | Read/update store data for user's applications | No |
| `bot` | Add bot to a guild | No |
| `connections` | Access linked third-party accounts | No |
| `dm_channels.read` | Read DM channel info | Approval |
| `email` | Access user's email address | No |
| `gdm.join` | Join group DMs on user's behalf | No |
| `guilds` | Access user's guilds (servers) | No |
| `guilds.join` | Join guilds on user's behalf | No |
| `guilds.members.read` | Read guild member info | No |
| `identify` | Access basic user info (username, avatar, etc.) | No |
| `messages.read` | Read messages from DMs and group DMs | Approval |
| `openid` | OIDC authentication | No |
| `relationships.read` | Access user's friend list | Approval |
| `role_connections.write` | Update user's connection/role metadata | No |
| `rpc` | Access RPC server | Approval |
| `rpc.activities.write` | Update user's activity via RPC | Approval |
| `rpc.notifications.read` | Receive notifications via RPC | Approval |
| `rpc.voice.read` | Read voice state | Approval |
| `rpc.voice.write` | Control voice via RPC | Approval |
| `voice` | Connect to voice channels | No |
| `webhook.incoming` | Create incoming webhook | No |

### Important Notes:
- Some scopes require Discord approval before use
- `bot` and `guilds.join` require a bot account linked to your application
- To add a user to a guild, your bot must already be in that guild
- Requesting unapproved scopes may cause errors in the OAuth2 flow

---

## Authorization URL

The authorization URL redirects users to Discord for authentication.

### URL Format

```
https://discord.com/api/oauth2/authorize?client_id=CLIENT_ID&redirect_uri=REDIRECT_URI&response_type=code&scope=SCOPES
```

### Required Parameters

| Parameter | Description |
|-----------|-------------|
| `client_id` | Your application's client ID |
| `redirect_uri` | Where Discord redirects after authorization (must match registered URI) |
| `response_type` | Either `code` (authorization code grant) or `token` (implicit grant) |
| `scope` | Space-delimited list of scopes (e.g., `identify email guilds`) |

### Optional Parameters

| Parameter | Description |
|-----------|-------------|
| `state` | CSRF protection - random string that's returned in callback |
| `prompt` | `consent` or `none` - controls re-authorization behavior |
| `permissions` | Permission integer for bot invites (with `bot` scope) |
| `guild_id` | Pre-fill guild selection for bot invites |
| `disable_guild_select` | Prevent user from changing guild (requires `guild_id`) |
| `integration_type` | `0` for guild install, `1` for user install |

### Example Authorization URLs

**Basic user authentication:**
```
https://discord.com/api/oauth2/authorize?client_id=YOUR_CLIENT_ID&redirect_uri=http%3A%2F%2Flocalhost%3A3000%2Fcallback&response_type=code&scope=identify%20email
```

**Bot invite with permissions:**
```
https://discord.com/api/oauth2/authorize?client_id=YOUR_CLIENT_ID&permissions=8&scope=bot&guild_id=123456789
```

---

## Token Exchange

After the user authorizes, Discord redirects to your redirect URI with a `code` parameter. Exchange this code for an access token.

### Request Format

**Endpoint**: `POST https://discord.com/api/oauth2/token`

**Content-Type**: `application/x-www-form-urlencoded` (JSON is not supported)

**Parameters**:

| Parameter | Description |
|-----------|-------------|
| `client_id` | Your application's client ID |
| `client_secret` | Your application's client secret |
| `grant_type` | `authorization_code` |
| `code` | The authorization code from the callback |
| `redirect_uri` | The same redirect URI used in authorization |

### Example Request (Node.js with undici)

```javascript
const { request } = require('undici');

async function exchangeCode(code) {
  const tokenResponseData = await request('https://discord.com/api/oauth2/token', {
    method: 'POST',
    body: new URLSearchParams({
      client_id: 'YOUR_CLIENT_ID',
      client_secret: 'YOUR_CLIENT_SECRET',
      code: code,
      grant_type: 'authorization_code',
      redirect_uri: 'http://localhost:3000/callback',
    }).toString(),
    headers: {
      'Content-Type': 'application/x-www-form-urlencoded',
    },
  });

  const oauthData = await tokenResponseData.body.json();
  return oauthData;
}
```

### Example Request (Python)

```python
import requests

API_ENDPOINT = 'https://discord.com/api/v10'
CLIENT_ID = 'YOUR_CLIENT_ID'
CLIENT_SECRET = 'YOUR_CLIENT_SECRET'
REDIRECT_URI = 'http://localhost:3000/callback'

def exchange_code(code):
    data = {
        'client_id': CLIENT_ID,
        'client_secret': CLIENT_SECRET,
        'grant_type': 'authorization_code',
        'code': code,
        'redirect_uri': REDIRECT_URI
    }
    headers = {
        'Content-Type': 'application/x-www-form-urlencoded'
    }
    r = requests.post(f'{API_ENDPOINT}/oauth2/token', data=data, headers=headers)
    r.raise_for_status()
    return r.json()
```

### Example Request (cURL)

```bash
curl -X POST https://discord.com/api/oauth2/token \
  -H "Content-Type: application/x-www-form-urlencoded" \
  -d "client_id=YOUR_CLIENT_ID&client_secret=YOUR_CLIENT_SECRET&grant_type=authorization_code&code=AUTHORIZATION_CODE&redirect_uri=http://localhost:3000/callback"
```

### Response Format

```json
{
  "access_token": "6qrZcUqja7812RVdnEKjpzOL4CvHBFG",
  "token_type": "Bearer",
  "expires_in": 604800,
  "refresh_token": "D43f5y0ahjqew82jZ4NViEr2YafMKhue",
  "scope": "identify guilds"
}
```

**Response Fields**:
- `access_token`: Use this to make authenticated API requests
- `token_type`: Always "Bearer"
- `expires_in`: Seconds until token expires (typically 604800 = 7 days)
- `refresh_token`: Use to get a new access token when it expires
- `scope`: Space-delimited scopes granted

---

## Refreshing Tokens

Access tokens expire after a period of time. Use the refresh token to get a new access token without re-prompting the user.

### Request Format

**Endpoint**: `POST https://discord.com/api/oauth2/token`

**Parameters**:

| Parameter | Description |
|-----------|-------------|
| `client_id` | Your application's client ID |
| `client_secret` | Your application's client secret |
| `grant_type` | `refresh_token` |
| `refresh_token` | The refresh token from the original token response |

### Example Request (Node.js)

```javascript
const { request } = require('undici');

async function refreshToken(refreshToken) {
  const tokenResponseData = await request('https://discord.com/api/oauth2/token', {
    method: 'POST',
    body: new URLSearchParams({
      client_id: 'YOUR_CLIENT_ID',
      client_secret: 'YOUR_CLIENT_SECRET',
      grant_type: 'refresh_token',
      refresh_token: refreshToken,
    }).toString(),
    headers: {
      'Content-Type': 'application/x-www-form-urlencoded',
    },
  });

  return await tokenResponseData.body.json();
}
```

### Example Request (Python)

```python
import requests

def refresh_token(refresh_token):
    data = {
        'client_id': CLIENT_ID,
        'client_secret': CLIENT_SECRET,
        'grant_type': 'refresh_token',
        'refresh_token': refresh_token,
    }
    headers = {
        'Content-Type': 'application/x-www-form-urlencoded'
    }
    r = requests.post(f'{API_ENDPOINT}/oauth2/token', data=data, headers=headers)
    r.raise_for_status()
    return r.json()
```

### Response

The response is identical to the initial token exchange response, including a new access token and refresh token.

---

## Making API Requests

Once you have an access token, include it in API requests using the Authorization header.

### Request Format

```
Authorization: Bearer YOUR_ACCESS_TOKEN
```

### Example: Get Current User

**Endpoint**: `GET https://discord.com/api/users/@me`

**Requires**: `identify` scope

```javascript
const { request } = require('undici');

async function getCurrentUser(accessToken) {
  const userResult = await request('https://discord.com/api/users/@me', {
    headers: {
      authorization: `Bearer ${accessToken}`,
    },
  });
  
  return await userResult.body.json();
}
```

**Response**:
```json
{
  "id": "80351110224678912",
  "username": "Nelly",
  "discriminator": "1337",
  "global_name": "Nelly",
  "avatar": "8342729096ea3675442027381ff50dfe",
  "verified": true,
  "email": "nelly@discord.com",
  "flags": 64,
  "banner": "06c16474723fe537c283b8efa61a30c8",
  "accent_color": 16711680,
  "premium_type": 1,
  "public_flags": 64
}
```

### Example: Get User's Guilds

**Endpoint**: `GET https://discord.com/api/users/@me/guilds`

**Requires**: `guilds` scope

```javascript
async function getUserGuilds(accessToken) {
  const guildsResult = await request('https://discord.com/api/users/@me/guilds', {
    headers: {
      authorization: `Bearer ${accessToken}`,
    },
  });
  
  return await guildsResult.body.json();
}
```

**Response**:
```json
[
  {
    "id": "80351110224678912",
    "name": "1337 Krew",
    "icon": "8342729096ea3675442027381ff50dfe",
    "owner": true,
    "permissions": "36953089",
    "features": ["COMMUNITY", "NEWS"]
  }
]
```

### Example: Get User's Connections

**Endpoint**: `GET https://discord.com/api/users/@me/connections`

**Requires**: `connections` scope

```javascript
async function getUserConnections(accessToken) {
  const connectionsResult = await request('https://discord.com/api/users/@me/connections', {
    headers: {
      authorization: `Bearer ${accessToken}`,
    },
  });
  
  return await connectionsResult.body.json();
}
```

---

## Security Considerations

### State Parameter (CSRF Protection)

Always use the `state` parameter to prevent Cross-Site Request Forgery (CSRF) attacks.

**Implementation**:
1. Generate a random string for each authorization request
2. Store it server-side (in session or database)
3. Include it in the authorization URL
4. Verify it matches when handling the callback

**Example**:

```javascript
const crypto = require('crypto');

// Generate state
function generateState() {
  return crypto.randomBytes(16).toString('hex');
}

// Store state (in session or database)
app.get('/login', (req, res) => {
  const state = generateState();
  req.session.oauth_state = state;
  
  const authUrl = `https://discord.com/api/oauth2/authorize?` +
    `client_id=${CLIENT_ID}&` +
    `redirect_uri=${encodeURIComponent(REDIRECT_URI)}&` +
    `response_type=code&` +
    `scope=identify email&` +
    `state=${state}`;
  
  res.redirect(authUrl);
});

// Verify state in callback
app.get('/callback', (req, res) => {
  const { code, state } = req.query;
  
  if (state !== req.session.oauth_state) {
    return res.status(403).send('State mismatch - possible CSRF attack');
  }
  
  // Proceed with token exchange
  // ...
});
```

### Best Practices

1. **Never expose Client Secret**: Keep it server-side only
2. **Use HTTPS**: Always use HTTPS in production
3. **Store tokens securely**: 
   - Use server-side sessions or encrypted database storage
   - Never store in local storage or cookies accessible to JavaScript
4. **Validate redirect URIs**: Ensure redirect URIs match exactly
5. **Handle token expiration**: Implement refresh token logic
6. **Limit scopes**: Only request scopes you actually need
7. **Rate limiting**: Respect Discord's rate limits

### Common Pitfalls

1. **Code can only be used once**: Authorization codes expire after one use
2. **HEAD requests**: Some frameworks send HEAD requests that can consume the code before the POST request
3. **Redirect URI mismatch**: Must exactly match what's registered in Discord Developer Portal
4. **Wrong Content-Type**: Token endpoint requires `application/x-www-form-urlencoded`

---

## Code Examples

### Complete Express.js Implementation

```javascript
const express = require('express');
const { request } = require('undici');
const crypto = require('crypto');
const session = require('express-session');

const app = express();
const CLIENT_ID = 'YOUR_CLIENT_ID';
const CLIENT_SECRET = 'YOUR_CLIENT_SECRET';
const REDIRECT_URI = 'http://localhost:3000/callback';
const PORT = 3000;

// Session middleware
app.use(session({
  secret: 'your-secret-key',
  resave: false,
  saveUninitialized: false,
  cookie: { secure: false } // Set to true in production with HTTPS
}));

// Home page
app.get('/', (req, res) => {
  if (req.session.user) {
    res.send(`
      <h1>Welcome, ${req.session.user.username}!</h1>
      <img src="https://cdn.discordapp.com/avatars/${req.session.user.id}/${req.session.user.avatar}.png" />
      <p><a href="/logout">Logout</a></p>
    `);
  } else {
    res.send('<h1>Discord OAuth2 Demo</h1><p><a href="/login">Login with Discord</a></p>');
  }
});

// Initiate OAuth2 flow
app.get('/login', (req, res) => {
  const state = crypto.randomBytes(16).toString('hex');
  req.session.oauth_state = state;
  
  const authUrl = `https://discord.com/api/oauth2/authorize?` +
    `client_id=${CLIENT_ID}&` +
    `redirect_uri=${encodeURIComponent(REDIRECT_URI)}&` +
    `response_type=code&` +
    `scope=identify email guilds&` +
    `state=${state}`;
  
  res.redirect(authUrl);
});

// OAuth2 callback
app.get('/callback', async (req, res) => {
  const { code, state } = req.query;
  
  // Verify state
  if (state !== req.session.oauth_state) {
    return res.status(403).send('State mismatch');
  }
  
  try {
    // Exchange code for token
    const tokenResponse = await request('https://discord.com/api/oauth2/token', {
      method: 'POST',
      body: new URLSearchParams({
        client_id: CLIENT_ID,
        client_secret: CLIENT_SECRET,
        code: code,
        grant_type: 'authorization_code',
        redirect_uri: REDIRECT_URI,
      }).toString(),
      headers: {
        'Content-Type': 'application/x-www-form-urlencoded',
      },
    });
    
    const oauthData = await tokenResponse.body.json();
    
    // Get user info
    const userResponse = await request('https://discord.com/api/users/@me', {
      headers: {
        authorization: `${oauthData.token_type} ${oauthData.access_token}`,
      },
    });
    
    const userData = await userResponse.body.json();
    
    // Store in session
    req.session.user = userData;
    req.session.tokens = oauthData;
    
    res.redirect('/');
  } catch (error) {
    console.error('OAuth error:', error);
    res.status(500).send('Authentication failed');
  }
});

// Logout
app.get('/logout', (req, res) => {
  req.session.destroy();
  res.redirect('/');
});

app.listen(PORT, () => {
  console.log(`Server running on http://localhost:${PORT}`);
});
```

### Client-Side HTML Example

```html
<!DOCTYPE html>
<html>
<head>
  <title>Discord OAuth2 Demo</title>
</head>
<body>
  <div id="content">
    <h1>Discord OAuth2 Demo</h1>
    <a id="login" href="#" style="display: none;">Login with Discord</a>
    <div id="user-info"></div>
  </div>

  <script>
    const CLIENT_ID = 'YOUR_CLIENT_ID';
    const REDIRECT_URI = 'http://localhost:3000';
    
    // Generate state for CSRF protection
    function generateRandomString() {
      let randomString = '';
      const randomNumber = Math.floor(Math.random() * 10);
      for (let i = 0; i < 20 + randomNumber; i++) {
        randomString += String.fromCharCode(33 + Math.floor(Math.random() * 94));
      }
      return randomString;
    }
    
    window.onload = () => {
      // Check for access token in URL fragment (implicit grant)
      const fragment = new URLSearchParams(window.location.hash.slice(1));
      const accessToken = fragment.get('access_token');
      const tokenType = fragment.get('token_type');
      
      if (!accessToken) {
        // No token, show login button
        const randomString = generateRandomString();
        localStorage.setItem('oauth-state', randomString);
        
        const loginUrl = `https://discord.com/api/oauth2/authorize?` +
          `client_id=${CLIENT_ID}&` +
          `redirect_uri=${encodeURIComponent(REDIRECT_URI)}&` +
          `response_type=token&` +
          `scope=identify&` +
          `state=${btoa(randomString)}`;
        
        document.getElementById('login').href = loginUrl;
        document.getElementById('login').style.display = 'block';
        return;
      }
      
      // Verify state
      const state = fragment.get('state');
      const storedState = localStorage.getItem('oauth-state');
      if (state !== btoa(storedState)) {
        console.error('State mismatch!');
        return;
      }
      
      // Fetch user info
      fetch('https://discord.com/api/users/@me', {
        headers: {
          authorization: `${tokenType} ${accessToken}`,
        },
      })
      .then(result => result.json())
      .then(response => {
        const { username, discriminator, id, avatar } = response;
        const avatarUrl = `https://cdn.discordapp.com/avatars/${id}/${avatar}.png`;
        
        document.getElementById('user-info').innerHTML = `
          <h2>Welcome, ${username}#${discriminator}!</h2>
          <img src="${avatarUrl}" alt="Avatar" />
        `;
      })
      .catch(console.error);
    };
  </script>
</body>
</html>
```

### Python Flask Implementation

```python
from flask import Flask, redirect, request, session, jsonify
import requests
import secrets
from urllib.parse import urlencode

app = Flask(__name__)
app.secret_key = 'your-secret-key'

CLIENT_ID = 'YOUR_CLIENT_ID'
CLIENT_SECRET = 'YOUR_CLIENT_SECRET'
REDIRECT_URI = 'http://localhost:5000/callback'
API_ENDPOINT = 'https://discord.com/api/v10'

@app.route('/')
def home():
    if 'user' in session:
        user = session['user']
        return f'''
            <h1>Welcome, {user['username']}!</h1>
            <img src="https://cdn.discordapp.com/avatars/{user['id']}/{user['avatar']}.png" />
            <p><a href="/logout">Logout</a></p>
        '''
    return '<h1>Discord OAuth2 Demo</h1><p><a href="/login">Login with Discord</a></p>'

@app.route('/login')
def login():
    state = secrets.token_urlsafe(16)
    session['oauth_state'] = state
    
    params = {
        'client_id': CLIENT_ID,
        'redirect_uri': REDIRECT_URI,
        'response_type': 'code',
        'scope': 'identify email guilds',
        'state': state
    }
    
    auth_url = f'https://discord.com/api/oauth2/authorize?{urlencode(params)}'
    return redirect(auth_url)

@app.route('/callback')
def callback():
    code = request.args.get('code')
    state = request.args.get('state')
    
    # Verify state
    if state != session.get('oauth_state'):
        return 'State mismatch', 403
    
    # Exchange code for token
    data = {
        'client_id': CLIENT_ID,
        'client_secret': CLIENT_SECRET,
        'grant_type': 'authorization_code',
        'code': code,
        'redirect_uri': REDIRECT_URI
    }
    headers = {
        'Content-Type': 'application/x-www-form-urlencoded'
    }
    
    r = requests.post(f'{API_ENDPOINT}/oauth2/token', data=data, headers=headers)
    r.raise_for_status()
    oauth_data = r.json()
    
    # Get user info
    headers = {
        'Authorization': f"{oauth_data['token_type']} {oauth_data['access_token']}"
    }
    r = requests.get(f'{API_ENDPOINT}/users/@me', headers=headers)
    r.raise_for_status()
    user_data = r.json()
    
    # Store in session
    session['user'] = user_data
    session['tokens'] = oauth_data
    
    return redirect('/')

@app.route('/logout')
def logout():
    session.clear()
    return redirect('/')

if __name__ == '__main__':
    app.run(debug=True, port=5000)
```

---

## Additional Resources

- [Discord Developer Portal](https://discord.com/developers/applications)
- [Discord API Documentation](https://discord.com/developers/docs)
- [OAuth 2.0 RFC 6749](https://tools.ietf.org/html/rfc6749)
- [discord.js OAuth2 Guide](https://discordjs.guide/oauth2/)

---

## Common Error Codes

| Error | Description | Solution |
|-------|-------------|----------|
| `invalid_client` | Client authentication failed | Check client ID and secret |
| `invalid_grant` | Invalid authorization code | Code expired or already used |
| `invalid_request` | Missing required parameter | Check request format |
| `invalid_scope` | Invalid or unapproved scope | Use approved scopes only |
| `redirect_uri_mismatch` | Redirect URI doesn't match | Ensure exact match with registered URI |
| `access_denied` | User denied authorization | User cancelled authorization |
| `unauthorized` | Invalid or expired token | Refresh token or re-authenticate |

---

## Rate Limits

Discord API has rate limits to prevent abuse. When using OAuth2 tokens:

- Most endpoints: 50 requests per second per token
- Specific endpoints may have stricter limits
- Rate limit information in response headers:
  - `X-RateLimit-Limit`: Max requests allowed
  - `X-RateLimit-Remaining`: Remaining requests
  - `X-RateLimit-Reset`: Unix timestamp when limit resets

**Best Practices**:
- Cache responses when possible
- Implement exponential backoff on 429 errors
- Monitor rate limit headers
- Use webhooks instead of polling when possible

---

This guide should provide everything you need to implement Discord OAuth2 in your application. Remember to always keep your client secret secure and follow security best practices!
