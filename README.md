# OdbcProvider for IIS
- This was written so that IIS can use an Odbc connection instead of MSSQL for MembershipProvider, RoleProvider, and ProfileProvider
- `database.sql` and `AddToTrustedProviders.ps1` have been added to aid in setup

## Setup
- Build
- Install to gac `gacutil /i OdbcProvider.dll`
- run `AddToTrustedProviders.ps1` and create the tables with `database.sql`
- copy to bin folder of site
- go through IIS dialogues to add a connection string, personally I create a (system) DSN then just use `DSN=[dsnname]`
- now you can add the Role and Membership providers, after this point you should be able to create users under the .Net Users page in IIS
