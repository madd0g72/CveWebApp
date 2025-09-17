# Production Admin Setup Guide

This guide explains how to set up and manage admin users in production environments.

## Default Admin User

When the application starts in production for the first time, it automatically creates a default admin user:

- **Email**: `admin@company.local`
- **Default Password**: `AdminPass1!`
- **Role**: Admin

### Important Security Notes

⚠️ **CRITICAL**: Change the default password immediately after first deployment!

## Password Requirements

### Development Environment
- Minimum length: 4 characters
- No special character requirements (for testing convenience)

### Production Environment
- Minimum length: 8 characters
- Must contain:
  - At least one digit
  - At least one lowercase letter
  - At least one uppercase letter
  - At least one special character

## Password Reset Process

Since email services are not configured in production environments, the application provides a self-service password reset mechanism:

### For Users Who Forgot Their Password

1. Go to the login page
2. Click "Forgot Password?"
3. Enter your email address
4. Click "Send Reset Link"
5. The system will display a direct reset link (no email required)
6. Click "Reset Password Now"
7. Enter your new password (must meet production requirements)
8. Confirm the password change

### Admin Password Reset

The admin user can reset their password using the same process:

1. Use email: `admin@company.local`
2. Follow the standard password reset process
3. Set a secure new password meeting production requirements

## First-Time Setup Checklist

When deploying to production:

- [ ] Start the application with `ASPNETCORE_ENVIRONMENT=Production`
- [ ] Verify the admin creation warning appears in logs: "Default admin user created: admin@company.local. Please change the password immediately!"
- [ ] Log in with default credentials (`admin@company.local` / `AdminPass1!`)
- [ ] Immediately change the admin password using the "Forgot Password" feature
- [ ] Test the new password by logging out and back in
- [ ] Verify admin functionality (Admin Tools dropdown should be available)

## Environment Differences

### Development Environment
- Admin: `admin@cveapp.local` / `admin123`
- User: `user@cveapp.local` / `user123`
- Relaxed password policies
- Test data and credentials displayed on login page

### Production Environment
- Admin: `admin@company.local` / `AdminPass1!` (change immediately)
- No test users created
- Strict password policies
- Production warning on login page
- Admin creation logged to console

## Troubleshooting

### Admin User Not Created
- Check that `ASPNETCORE_ENVIRONMENT=Production` is set
- Look for the warning message in application logs
- Verify database connectivity

### Cannot Login
- Ensure you're using the correct email: `admin@company.local`
- Check that the password meets production requirements
- Use "Forgot Password" to reset if needed

### Password Reset Not Working
- Verify the application is running in the correct environment
- Check that the email address is correct
- Ensure the new password meets all production requirements

## Additional Admin Users

To create additional admin users:

1. Log in as the default admin
2. Use the admin tools to create new users (if user management is implemented)
3. Or create them directly in the database with Admin role assignment

## Security Best Practices

1. **Change Default Password**: Always change the default admin password immediately
2. **Strong Passwords**: Use passwords that exceed the minimum requirements
3. **Limited Access**: Only create admin users as needed
4. **Regular Updates**: Regularly update admin passwords
5. **Monitoring**: Monitor admin login attempts and activities