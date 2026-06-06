Feature: Data Access Layer (DAL)
    BD to write a sufficiently impressive description. The DAL is generic database functions. Connecting, executing scalar, etc.

    # 1. Connection string handling
    
    @FT-DAL-1.1 The environment variable LEOBLOOM_ENV must be in place or all data access functions must fail with an error
    @FT-DAL-1.2 The environment variable LEOBLOOM_ENV will be used to determine which external configuration file to use (Production vs Development vs...)
    @FT-DAL-1.3 If the external configuration file cannot be accessed by the system, all data access functions must fail with an error
    @FT-DAL-1.4 The external configuration file must define a connection string named "SonOfLeo" that the system must use to connect to the database
    @FT-DAL-1.5 If the system cannot access the "SonOfLeo" connection string configuration, all data access functions must fail with an error 
    @FT-DAL-1.6 If the "SonOfLeo" connection string configuration is empty or all white space, all data access functions must fail with an error    
    @FT-DAL-1.7 The environment variable LEOBLOOM_DB_PASSWORD must be in place or all data access functions must fail with an error
    @FT-DAL-1.8 The "SonOfLeo" connection string will not print the database password in the external configuration file (untestable)
    @FT-DAL-1.9 The system will "inject" the environment variable LEOBLOOM_DB_PASSWORD contents into the final connection string at run-time
    @FT-DAL-1.10 The system will trim leading and trailing white space from the LEOBLOOM_DB_PASSWORD environment variable
    @FT-DAL-1.11 If the trimmed LEOBLOOM_DB_PASSWORD environment variable is empty, all data access functions must fail with an error
    @FT-DAL-1.12 The system will trim leading and trailing white space from the LEOBLOOM_ENV environment variable
    @FT-DAL-1.13 If the trimmed LEOBLOOM_ENV environment variable is empty, all data access functions must fail with an error
    