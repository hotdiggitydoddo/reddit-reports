# Notes:
## Regarding API keys:
- `client_secret` config value must be read from an environment variable named "CLIENT_SECRET" on the machine the application is running on
  - For demonstration purposes, it is currently included in the launchSettings.json file in the console application project folder and will work when the application is launched from Visual Studio.
 ## Authenticating with Reddit
 - Authentication is handled via OAuth2 using `code` for the `grant-type`. The application will provide instructions and a link on startup for you which will kick off the oauth flow.
   - The link will ask you to log into Reddit (you must have an account) to grant the application permission to call Reddit's API and pull information
   - Upon granting permission, the browser should redirect to the dummy URL `https://www.example.com` with the auth code appended to the query string.  Copy and paste the whole URL back into the application window.
