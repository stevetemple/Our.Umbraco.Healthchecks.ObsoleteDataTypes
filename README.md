# Our.Umbraco.Healthchecks.ObsoleteDataTypes

A set of healthchecks for dealing with obsolete data types that will not be supported in v8 of Umbraco. These aren't traditional healthchecks but more using the healtcheck system as a convenient place to check and run these from.

The intention is to install this into a V7 site and use the healthcheck to migrate obsolete data types and the related content prior to an upgrade to v8+.

*This should not be used on a production site as these changes will update the database, but will not update the code relying on the data, you'll need to combine running these with a code update.*

Current data types flagged as obsolete:

Member Picker
Multi Node Tree Picker
Related Links

Flagged as obsolete with the option to "fix":

Archetype => Nested Content (All the different settings are not fully supported, this was adequate for my uses)
Our.Umbraco.NestedContent => Nested Content