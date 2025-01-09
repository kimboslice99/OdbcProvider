# OdbcProvider for IIS
- This was written so that IIS can use an Odbc connection instead of MSSQL for MembershipProvider, RoleProvider, and ProfileProvider

## Setup
- Build
- Install to gac `gacutil /i OdbcProvider.dll`
- create a database for your connection string
- go through IIS dialogues to add a connection string (custom), personally I create a (system) DSN then just use `DSN=[dsnname]`
- To use COM wrapper (convenient for scripting) `C:\Windows\Microsoft.NET\Framework64\v4.0.30319\regasm OdbcProvider.dll /codebase`
- run `AddToTrustedProviders.ps1`
- copy to bin folder of site
- now you can add the Role and Membership providers, after this point you should be able to create users under the .Net Users page in IIS

##
if registering COM, here is an example of usage. currently not much is passed through the wrapper. more to come.
```php
<?php
$membershipCom = new COM('OdbcProvider.OdbcProviderWrapper');
$membershipCom->SetConnectionString("DSN=YOUR_DSN");
$membershipCom->IsUserInRole($_SERVER['REMOTE_USER'], "RoleName"); // bool
$membershipCom->ValidateUser("username", "password")
$membershipCom->UnlockUser("username")
```
