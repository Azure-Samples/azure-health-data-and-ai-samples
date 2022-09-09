# Databricks notebook source
# TESTING CELL
# spark.table("fhir.Observation_Cleaned").display()

# COMMAND ----------

# MAGIC %md ## Genereal Helper Functions

# COMMAND ----------

def mount_storage(container_name, storage_account_name, mount_name, client_id, client_secret, tenant_id):
    # Change the Databricks secret scope and keys to match your environment setup
    configs = {"fs.azure.account.auth.type": "OAuth",
        "fs.azure.account.oauth.provider.type": "org.apache.hadoop.fs.azurebfs.oauth2.ClientCredsTokenProvider",
        "fs.azure.account.oauth2.client.id": client_id,
        "fs.azure.account.oauth2.client.secret": client_secret,
        "fs.azure.account.oauth2.client.endpoint": "https://login.microsoftonline.com/%s/oauth2/token" % tenant_id}
    
    mount_path = "/mnt/%s" % mount_name
    if any(mount.mountPoint == mount_path for mount in dbutils.fs.mounts()):
        dbutils.fs.unmount(mount_path)

    dbutils.fs.mount(
        source = "abfss://%s@%s.dfs.core.windows.net/" % (container_name, storage_account_name),
        mount_point = mount_path,
        extra_configs = configs)

# COMMAND ----------

# Autoloader doesn't infer schema for parquet files yet. We can pull the schema from the source files and export them to our data lake for use in our pipelines.

def extract_schemas(mount_name):
    resources = dbutils.fs.ls('dbfs:/mnt/%s/result/' % mount_name)
    for resource in resources:
        resource_name = resource.name.rstrip('/')
        df = spark.read.option("header","true").option("recursiveFileLookup","true").parquet("dbfs:/mnt/%s/result/%s/" % (mount_name, resource_name))
        schema = df.schema
        dbutils.fs.put('dbfs:/mnt/%s/auto-loader/schema/%s.json' % (mount_name, resource_name), schema.json(), True)

# COMMAND ----------

import json
from pyspark.sql.types import StructType, StructField


schema_dir = 'dbfs:/mnt/%s/auto-loader/schema/' % mount_name
schema_rdd = spark.sparkContext.wholeTextFiles(schema_dir)
schema_rdd_collected = schema_rdd.collect()

def get_resource_schema(resource_type: str) -> StructType:
  resource_rows = list(filter(lambda x: (x[0].endswith("%s.json" % resource_type)), schema_rdd_collected))
  if (len(resource_rows) != 1):
    raise Exception(f"Expected one row in get_resource_schema for {resource_type}. Got {str(len(resource_rows))}.")

  return StructType.fromJson(json.loads(resource_rows[0][1]))

# COMMAND ----------

# MAGIC %md
# MAGIC 
# MAGIC ## Silver: Cleaning and Filtering Scripts for Silver
# MAGIC 
# MAGIC In the Silver layer of the lakehouse, the data from the Bronze layer is matched, merged, conformed and cleansed ("just-enough") so that the Silver layer can provide an "Enterprise view" of all its key business entities, concepts and transactions. (e.g. master customers, stores, non-duplicated transactions and cross-reference tables).
# MAGIC 
# MAGIC Here, we are flattening our FHIR data to a more tabular format that matches our enterprise schema.
# MAGIC 
# MAGIC In a production deployment, this code should be extracted into a library that can be unit tested.

# COMMAND ----------

from pyspark.sql import DataFrame, Column
import pyspark.sql.functions as F

#################################
# HELPER FUNCTIONS
#################################

def first_identifier_value_by_system(system : str) -> Column:
    return F.filter('identifier', lambda x: x['system'] == system)[0]['value']

def first_official_name() -> Column:
    return F.transform(
        F.filter('name', lambda x: x['use'] == 'official'),
        lambda x: x.withField('first_name', x['given'][0]).withField('last_name', x['family'])
    )[0]

def first_phome_by_use(use : str) -> Column:
    return F.filter('telecom', lambda x: ((x['system'] == 'phone') & (x['use'] == use)))[0]['value']

def email_value() -> Column:
    return F.transform(F.filter('telecom', lambda x: x['system'] == 'email'), lambda x: x.value)[0]

def resource_id_from_path(resource_type: str, path: str) -> Column:
    return F.expr(f'if(substr({path}, 1, {len(resource_type) + 1}) == "{resource_type}/", substr({path}, {len(resource_type) + 2}), null)')

#################################
# Patient cleaning logic
#################################

def clean_patient_df(df : DataFrame) -> DataFrame:
    return (df
        .withColumn('synthea_identifier', first_identifier_value_by_system('https://github.com/synthetichealth/synthea'))
        .withColumn('official_name', first_official_name())
        .withColumn('home_phone', first_phome_by_use('home'))
        .withColumn('mobile_phone', first_phome_by_use('mobile'))
        .withColumn('email', email_value())
        .withColumn('first_address', F.col('address')[0])
        .withColumn('deceased_bool', F.expr('deceased.boolean is not null OR deceased.dateTime is not null as deceased'))
        .selectExpr(
            'resourceType',
            'id',
            'meta.versionId',
            'meta.lastUpdated',
            'synthea_identifier',
            'official_name',
            'official_name.first_name',
            'official_name.last_name',
            'home_phone',
            'mobile_phone',
            'email',
            'gender',
            'birthDate',
            'first_address',
            'first_address.line[0] as address_line_1',
            'first_address.line[1] as address_line_2',
            'first_address.line[2] as address_line_3',
            'first_address.city as address_city',
            'first_address.state as address_state',
            'first_address.postalCode as address_zip',
            'maritalStatus.coding[0].code as marital_status',
            'deceased_bool as deceased',
            'deceased.dateTime'
        )
    )

#################################
# Encounter cleaning logic
#################################

def clean_encounter_df(df : DataFrame) -> DataFrame:
    return (df
        .withColumn('synthea_identifier', first_identifier_value_by_system('https://github.com/synthetichealth/synthea'))
        .withColumn('patient_id', resource_id_from_path('Patient', 'subject.reference'))
        .selectExpr(
            'resourceType',
            'id',
            'meta.versionId',
            'meta.lastUpdated',
            'synthea_identifier',
            'class.code as class',
            'type[0].coding[0].code as type',
            'patient_id',
            'participant[0].individual.reference as practitioner',
            'period.start as start_date_time',
            'period.end as end_date_time'
        )
    )
    
#################################
# Observation cleaning logic
#################################

def extract_codes(column_name: str) -> Column:
    return F.transform(
        F.col(column_name)['coding'],
        lambda x: x.dropFields('id', 'extension', 'system', 'version', 'userSelected')
    )

def extract_codes_with_system(column_name: str, system: str) -> Column:
    return F.transform(
        F.filter(F.col(column_name)['coding'], lambda x: x['system'] == system),
        #lambda x: x.selectExpr('code', 'display', 'system')
        lambda x: x.dropFields('id', 'extension', 'system', 'version', 'userSelected')
    )
    
def extract_observation_values() -> Column:
    return F.coalesce(
        F.transform(F.col('component'), lambda x: x['value']['quantity']),
        F.array(F.col('value')['quantity'])
    )

def clean_observation_df(df : DataFrame) -> DataFrame:
    return (df
        .withColumn('extracted_codes', extract_codes_with_system('code', 'http://loinc.org'))
        .withColumn('patient_id', resource_id_from_path('Patient', 'subject.reference'))
        .withColumn('encounter_id', resource_id_from_path('Encounter', 'encounter.reference'))
        .withColumn('value_quantity', extract_observation_values())
        .withColumn('value_code', extract_codes('value.codeableConcept'))
        .selectExpr(
            'resourceType',
            'id',
            'meta.versionId',
            'meta.lastUpdated',
            'status',
            'category[0].coding[0].code as category',
            'extracted_codes',
            'patient_id',
            'encounter_id',
            'issued',
            'value_quantity',
            'value_code'
        )
    )

# Uncomment to test outside of delta live tables in this notebook
#clean_patient_df(spark.table("fhir.patient_raw")).display()
#clean_encounter_df(spark.table("fhir.encounter_raw")).display()
#clean_observation_df(spark.table("fhir.observation_raw")).display()

# COMMAND ----------

# MAGIC %md ## Gold Level Data Transformations

# COMMAND ----------

import pyspark.sql.functions as F
from pyspark.sql.types import TimestampType
from pyspark.sql import DataFrame, window

def create_height_patient_observation_df(patientDf: DataFrame, observationDf: DataFrame):
    observationFilteredDf = observationDf \
        .filter(F.size(F.filter(F.col('extracted_codes'), lambda x: x['code'] == '8302-2')) == 1)
    joinedDf = (
        observationFilteredDf
            .join(other=patientDf, on=patientDf.id == observationDf.patient_id, how='inner')
    ) \
    .withColumn('timestamp', observationFilteredDf['lastUpdated'].cast(TimestampType())) \
    .withColumn('height', F.transform(F.col('value_quantity'), lambda x: x.value)[0]) \
    .withColumn('age', F.expr('int(months_between(issued, birthDate) / 12)')) \
    .withColumn('patient_city', patientDf['address_city']) \
    .selectExpr(
        'timestamp',
        'int(height / 10) * 10 as height_range',
        'int(age / 10) * 10 as age_range',
        'patient_city'
    ) \
    .withWatermark("timestamp", "15 minutes") \
    .groupBy(
            F.window(F.col('timestamp'), "15 minutes"),
            'age_range',
            'height_range',
            'patient_city'
    ) \
    .count() \
    .orderBy(['window.start', 'age_range', 'height_range', 'patient_city'], ascending=[False, True, True, True])

    return joinedDf

# Uncomment to test outside of delta live tables in this notebook
# create_height_patient_observation_df(
#    spark.table("fhir.patient_cleaned"), 
#    spark.table("fhir.observation_cleaned")
# ).display()
