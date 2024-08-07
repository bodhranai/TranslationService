terraform {
  required_providers {
    azurerm = {
      source = "hashicorp/azurerm"
      version = "3.114.0"
    }
  }
}
provider "azurerm" {
 features{

 }
}

resource "azurerm_resource_group" "language_translator_rg" {
  name     = "language-translator-rg"
  location = "westus"
}
resource "azurerm_cognitive_account" "text_translation" {
  name                = "text-translation"
  location            = azurerm_resource_group.language_translator_rg.location
  resource_group_name = azurerm_resource_group.language_translator_rg.name
  kind                = "TextTranslation"
  sku_name            = "F0"
}
output "text_translation_key" {
  value = azurerm_cognitive_account.text_translation.primary_access_key
  sensitive=true
}
output "text_translation_endpoint" {
  value = azurerm_cognitive_account.text_translation.endpoint
  sensitive=true
}

resource "azurerm_storage_account" "language_translator-stg" {
  name                     = "languagetranslatorstg"
  resource_group_name      = azurerm_resource_group.language_translator_rg.name
  location                 = azurerm_resource_group.language_translator_rg.location
  account_tier             = "Standard"
  account_replication_type = "LRS"
}
resource "azurerm_service_plan" "language-translator-appsvcplan" {
  name                = "language-translator-appsvcplan"
  location            = azurerm_resource_group.language_translator_rg.location
  resource_group_name = azurerm_resource_group.language_translator_rg.name
  sku_name = "B1" 
  os_type= "Windows"
}
resource "azurerm_windows_function_app" "language_translator-fxn" {
  name                 = "language-translator-fxn"
  location             = azurerm_resource_group.language_translator_rg.location
  resource_group_name  = azurerm_resource_group.language_translator_rg.name
  service_plan_id  = azurerm_service_plan.language-translator-appsvcplan.id
  storage_account_name = azurerm_storage_account.language_translator-stg.name
  storage_account_access_key = azurerm_storage_account.language_translator-stg.primary_access_key
  site_config {
     always_on = true
  }
  app_settings = {
      "TextTranslationKey" = azurerm_cognitive_account.text_translation.primary_access_key
      "TextTranslationEndpoint" = azurerm_cognitive_account.text_translation.endpoint
       "FUNCTIONS_WORKER_RUNTIME"      = "dotnet"
         "AzureWebJobsStorage"           = azurerm_storage_account.language_translator-stg.primary_connection_string
          "StorageContainerName"          = azurerm_storage_container.language_translator-cont.name
    "APPINSIGHTS_INSTRUMENTATIONKEY" = azurerm_application_insights.language_translator-appinsights.instrumentation_key

   
  }
  
  identity {
    type = "SystemAssigned"
  }
}


resource "azurerm_storage_container" "language_translator-cont" {
  name                  = "translations"
  storage_account_name  = azurerm_storage_account.language_translator-stg.name
  container_access_type = "private"
}
resource "azurerm_application_insights" "language_translator-appinsights" {
  name                = "language-translator-app-insights"
  location            = azurerm_resource_group.language_translator_rg.location
  resource_group_name = azurerm_resource_group.language_translator_rg.name
  application_type="web"
}
output "function_app_endpoint" {
  value = azurerm_windows_function_app.language_translator-fxn.default_hostname
}