# Set the path to the administration.config file
$adminConfigPath = "$env:SystemRoot\System32\inetsrv\config\administration.config"

# Define the XML node structure
$membershipNode = @"
<add type="OdbcProvider.OdbcMembershipProvider, OdbcProvider, Version=1.0.0.0, Culture=neutral, PublicKeyToken=a96b8dd2462a65fd" />
"@

$roleNode = @"
<add type="OdbcProvider.OdbcRoleProvider, OdbcProvider, Version=1.0.0.0, Culture=neutral, PublicKeyToken=a96b8dd2462a65fd" />
"@

# Load the configuration file
$xml = [xml](Get-Content $adminConfigPath)

# Find the <membership> and <roleManager> nodes
$membershipSection = $xml.SelectSingleNode("//system.webServer/management/trustedProviders")
$roleSection = $xml.SelectSingleNode("//system.webServer/management/trustedProviders")

# Append the new nodes
$membershipSection.InnerXml += $membershipNode
$roleSection.InnerXml += $roleNode

# Save the changes
$xml.Save($adminConfigPath)
