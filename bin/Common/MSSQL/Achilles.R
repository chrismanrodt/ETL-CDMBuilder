### package setup steps
install.packages("devtools", repos = "http://cran.us.r-project.org")
library(devtools)

# explicit packages install are needed to install dependencies
install.packages("rJava", repos=c("http://rstudio.org/_packages", "http://cran.rstudio.com")) 
install.packages("DBI", repos=c("http://rstudio.org/_packages", "http://cran.rstudio.com")) 
install.packages("fastmatch", repos=c("http://rstudio.org/_packages", "http://cran.rstudio.com")) 

if (Sys.getenv("JAVA_HOME")!="")
  Sys.setenv(JAVA_HOME="")

install_github("OHDSI/SqlRender")
install_github("OHDSI/DatabaseConnector")
#install_github("OHDSI/Achilles")
install_github(repo = "OHDSI/Achilles", ref = "1.3.0")
library(Achilles)
library(SqlRender)

### Fill these out
loadId <- "{loadId}" # specify HIX database load id
rCluster <- "{rsCluster}" # adjust this cluster name
rUser <- "{rsUser}" # adjust this user name
rPassword <- "{rsPassword}" # adjust this password
cdmDatabaseSchema = "{rsDbSchema}" # should stay as-is
db <- "{rsDbName}" # adjust this database name
hixServer <- "{hixServer}"
hixDb <- "{hixDb}"
hixPort <- {hixPort}
hixUser <- "{hixUser}"
hixPass <- "{hixPassword}"

# grab source name from HIX
sourceNameSql <- "select case
					when C.Name = D.Name then D.Name + ' v' + cast(A.loadId as varchar)
					else D.Name + ' ' + C.Name + ' v' + cast(A.loadId as varchar)
					end as cdm_name from HIX.dbo.load A
					join HIX.dbo.load B on A.LineageId = B.LoadId
					join HIX.dbo.VendorSchema C on B.VendorSchemaId = C.VendorSchemaId
					join HIX.dbo.Vendor D on C.VendorId = D.VendorId
					where A.loadid = @loadId;"
renderedSql <- renderSql(sql = sourceNameSql, loadId = loadId)$sql
connectionDetails <- createConnectionDetails(dbms = "sql server", server = hixServer, port = hixPort, user = hixUser, password = hixPass)
connection <- connect(connectionDetails)
sourceName <- querySql(connection = connection, sql = renderedSql)
dbDisconnect(connection)

outputRoot <- "{achillesJsonFolder}" # need to put a root folder for JSON file exports, use forward slashes
runCostAnalysis <- {runCostAnalysis} # this should be a parameter in the wrapper or UI
cdmVersion <- "{cdmVersion}" # this should be a parameter in the wrapper or UI
resultsDatabaseSchema <- "{resultsSchema}" ### edited to use ohdsi_results as location to write to

jsonPath <- paste0(outputRoot, "/", paste(db, loadId, sep = "_", collapse = ""))
unlink(x = jsonPath, recursive = TRUE, force = TRUE) 

cdmDatabaseSchema <- paste(db, ".", resultsDatabaseSchema, sep="", collapse="")

connectionDetails <- createConnectionDetails (dbms="sql server", server=rCluster, port=1433, user=rUser, password = rPassword)

achillesResults <- achilles(connectionDetails, cdmDatabaseSchema = cdmDatabaseSchema, runCostAnalysis = runCostAnalysis,
                            resultsDatabaseSchema = resultsDatabaseSchema, sourceName = sourceName,
                            cdmVersion = cdmVersion, vocabDatabaseSchema = cdmDatabaseSchema)


exportToJson(connectionDetails, cdmDatabaseSchema = cdmDatabaseSchema,
             resultsDatabaseSchema = resultsDatabaseSchema,
             outputPath = jsonPath,
             cdmVersion = cdmVersion, vocabDatabaseSchema = cdmDatabaseSchema)
