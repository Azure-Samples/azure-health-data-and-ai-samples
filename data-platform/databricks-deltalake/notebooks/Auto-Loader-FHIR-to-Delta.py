# Databricks notebook source
# DBTITLE 1,Mount FHIR to Data Lake Storage Account
###################################################
# First we need to mount the Azure Data Dake where
# FHIR to Datalake exports the data from the FHIR 
# service.
###################################################

def mount_storage(container_name, storage_account_name, mount_name, client_id, client_secret, tenant_id):
    spark.conf.set(
      "fs.azure.account.key.<storage-account>.dfs.core.windows.net",
      dbutils.secrets.get(scope="<scope>", key="<storage-account-access-key>")
    )

    mount_path = "/mnt/%s" % mount_name
    if any(mount.mountPoint == mount_path for mount in dbutils.fs.mounts()):
        dbutils.fs.unmount(mount_path)

    dbutils.fs.mount(
        source = "abfss://%s@%s.dfs.core.windows.net/" % (container_name, storage_account_name),
        mount_point = mount_path,
        extra_configs = configs)
 
# Setup our mount functions
container_name = "fhir"
storage_account_name = dbutils.secrets.get(scope="sample-secrets", key="adls-storage-account-name")
mount_name = "fhir"
client_id = dbutils.secrets.get(scope="sample-secrets", key="adls-access-client-id")
client_secret = dbutils.secrets.get(scope="sample-secrets", key="adls-access-client-secret")
tenant_id = dbutils.secrets.get(scope="sample-secrets", key="adls-access-tenant-id")

# Call mount function
mount_storage(container_name, storage_account_name, mount_name, client_id, client_secret, tenant_id)


# COMMAND ----------

# DBTITLE 1,Save Schema from Parquet Files for Auto Loader
###################################################
# Autoloader doesn't infer schema for parquet 
# we need to save off existing schema for later use
###################################################

resources = dbutils.fs.ls('dbfs:/mnt/%s/result/' % mount_name)
for resource in resources:
    resource_name = resource.name.rstrip('/')
    df = spark.read.option("header","true").option("recursiveFileLookup","true").parquet("dbfs:/mnt/%s/result/%s/" % (mount_name, resource_name))
    schema = df.schema
    dbutils.fs.put('dbfs:/mnt/%s/auto-loader/schema/%s.json' % (mount_name, resource_name), schema.json(), True)
    

# COMMAND ----------

# DBTITLE 1,Fetch Patient Schema
import json
from pyspark.sql.types import StructType

patient_schema_file = 'dbfs:/mnt/%s/auto-loader/schema/Patient.json' % mount_name
patient_schema_rdd = spark.sparkContext.wholeTextFiles(patient_schema_file)
patient_schema_json = patient_schema_rdd.collect()[0][1]

# Load Schema
patient_schema = StructType.fromJson(json.loads(patient_schema_json))

# COMMAND ----------

# DBTITLE 1,Create Patient Stream from FHIR to Data Lake
patient_raw_data_location = "/mnt/fhir/result/Patient"

patient_stream = (spark.readStream
  .format("cloudFiles")
  .schema(patient_schema)
  .option("cloudFiles.format", "parquet")
  .load(patient_raw_data_location))


# COMMAND ----------

# DBTITLE 1,Display Streaming Data (real-time)
display(patient_stream)

# COMMAND ----------

# DBTITLE 1,Write Stream to Target Delta Lake
patient_target_delta_table_location = "/mnt/fhir/delta/Patient"
checkpoint_location = "/mnt/fhir/auto-loader/checkpoint"

patient_stream.writeStream \
  .option("checkpointLocation", checkpoint_location) \
  .start(patient_target_delta_table_location)

# COMMAND ----------

# DBTITLE 1,Create Patient Table
table_name = 'Patient' 
display(spark.sql("DROP TABLE IF EXISTS " + table_name))
display(spark.sql("CREATE TABLE " + table_name + " USING DELTA LOCATION '" + patient_target_delta_table_location + "'"))

# COMMAND ----------

# DBTITLE 1,Show Patient Table Information
display(spark.table(table_name).select('id', 'gender'))
df_patient = spark.table(table_name)
display(df_patient.select('gender').orderBy('gender', ascending = False).groupBy('gender').count())
