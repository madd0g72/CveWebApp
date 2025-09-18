# Active Directory Authentication Configuration

The CVE Web Application supports Active Directory authentication using secure LDAP (LDAPS) connections. This document provides configuration instructions and best practices.

## Overview

The AD authentication system provides:
- Secure LDAPS-only connections to Active Directory
- Service account-based LDAP binds
- Automatic user provisioning from AD
- Role mapping based on AD group membership
- No password reset functionality for AD users (managed externally)
- Fallback to local authentication (configurable)

## Configuration

### appsettings.json Configuration

Add the following section to your `appsettings.json` file:

```json
{
  "ActiveDirectory": {
    "Enabled": true,
    "Server": "dc.company.local",
    "Port": 636,
    "UseSsl": true,
    "BaseDn": "CN=Users,DC=company,DC=local",
    "Domain": "company.local",
    "ServiceAccountUsername": "cveapp-service@company.local",
    "ServiceAccountPassword": "SECURE_PASSWORD_HERE",
    "UserSearchFilter": "(&(objectClass=user)(sAMAccountName={0}))",
    "DisplayNameAttribute": "displayName",
    "EmailAttribute": "mail",
    "GroupMembershipAttribute": "memberOf",
    "TimeoutSeconds": 30,
    "AdminGroupDn": "CN=CVE-Admins,CN=Users,DC=company,DC=local",
    "UserGroupDn": "CN=CVE-Users,CN=Users,DC=company,DC=local",
    "AllowLocalUserFallback": true
  }
}
```

### Configuration Properties

| Property | Description | Default | Required |
|----------|-------------|---------|----------|
| `Enabled` | Whether AD authentication is enabled | `false` | Yes |
| `Server` | LDAP server hostname or IP | - | Yes |
| `Port` | LDAP port (636 for LDAPS) | `636` | No |
| `UseSsl` | Use SSL/TLS for connections | `true` | No |
| `BaseDn` | Base DN for user searches | - | Yes |
| `Domain` | Domain name | - | Yes |
| `ServiceAccountUsername` | Service account for LDAP binds | - | Yes |
| `ServiceAccountPassword` | Service account password | - | Yes |
| `UserSearchFilter` | LDAP filter for user searches | `(&(objectClass=user)(sAMAccountName={0}))` | No |
| `DisplayNameAttribute` | Attribute for user's display name | `displayName` | No |
| `EmailAttribute` | Attribute for user's email | `mail` | No |
| `GroupMembershipAttribute` | Attribute for group membership | `memberOf` | No |
| `TimeoutSeconds` | Connection timeout | `30` | No |
| `AdminGroupDn` | AD group for Admin role | - | No |
| `UserGroupDn` | AD group for User role | - | No |
| `AllowLocalUserFallback` | Allow local authentication fallback | `true` | No |

## Active Directory Setup

### 1. Service Account Creation

Create a dedicated service account in Active Directory:

```powershell
# Create service account
New-ADUser -Name "CVE App Service" -SamAccountName "cveapp-service" -UserPrincipalName "cveapp-service@company.local" -AccountPassword (ConvertTo-SecureString "SECURE_PASSWORD" -AsPlainText -Force) -Enabled $true

# Set password to never expire
Set-ADUser -Identity "cveapp-service" -PasswordNeverExpires $true

# Grant read permissions to the Users container
dsacls "CN=Users,DC=company,DC=local" /G "company\cveapp-service:GR"
```

### 2. Security Groups

Create security groups for role mapping:

```powershell
# Create admin group
New-ADGroup -Name "CVE-Admins" -GroupScope Global -GroupCategory Security -Path "CN=Users,DC=company,DC=local"

# Create user group  
New-ADGroup -Name "CVE-Users" -GroupScope Global -GroupCategory Security -Path "CN=Users,DC=company,DC=local"

# Add users to groups
Add-ADGroupMember -Identity "CVE-Admins" -Members "admin.user"
Add-ADGroupMember -Identity "CVE-Users" -Members "regular.user"
```

### 3. Certificate Configuration

For production environments, ensure proper SSL certificates are configured on your domain controllers for LDAPS.

## Authentication Flow

1. User submits credentials via login form
2. If AD is enabled, attempt AD authentication first:
   - Service account binds to AD
   - User lookup by sAMAccountName
   - User credential validation via LDAP bind
   - Group membership retrieval
3. If AD authentication succeeds:
   - User is provisioned/updated in local database
   - Roles are assigned based on AD groups
   - User is signed in
4. If AD authentication fails and local fallback is enabled:
   - Attempt local database authentication
5. If both fail, display error message

## Role Mapping

- Users in `AdminGroupDn` receive the "Admin" role
- Users in `UserGroupDn` receive the "User" role
- Users not in specified groups receive the default "User" role
- Multiple roles can be assigned if user is in multiple groups

## Security Features

### LDAPS Only
- All connections use SSL/TLS encryption
- Port 636 (LDAPS) is the default
- Plain LDAP connections are not supported

### Service Account Security
- Dedicated service account with minimal permissions
- Read-only access to user container
- Password stored securely in configuration

### Password Management
- AD users cannot reset passwords through the application
- Password reset forms detect AD users and prevent reset attempts
- Users are directed to contact system administrators

## Development vs Production

### Development Environment
- AD authentication can be disabled for testing
- Local fallback credentials available
- Less restrictive configuration validation

### Production Environment
- AD authentication typically enabled
- Secure service account credentials required
- Enhanced logging for authentication events

## Troubleshooting

### Common Issues

1. **Connection Timeouts**
   - Verify network connectivity to domain controller
   - Check firewall rules for port 636
   - Verify SSL certificate validity

2. **Authentication Failures**
   - Verify service account credentials
   - Check service account permissions
   - Validate BaseDn and search filters

3. **Role Assignment Issues**
   - Verify group DNs are correct
   - Check user group memberships in AD
   - Review application logs for role assignment

### Logging

The application logs AD authentication events:

- Successful authentications
- Failed authentication attempts
- User provisioning actions
- Role assignments
- Configuration errors

Logs are available in the configured logging location.

## Best Practices

1. **Security**
   - Use dedicated service account with minimal permissions
   - Regularly rotate service account password
   - Monitor authentication logs
   - Use secure LDAPS connections only

2. **Performance**
   - Set appropriate timeout values
   - Consider connection pooling for high-traffic scenarios
   - Monitor AD server performance

3. **Maintenance**
   - Keep service account password updated
   - Review and update group memberships regularly
   - Test authentication after AD infrastructure changes

## Support

For additional support:
- Review application logs for detailed error messages
- Verify AD configuration with domain administrators
- Test connectivity using LDAP utilities
- Consult Microsoft Active Directory documentation