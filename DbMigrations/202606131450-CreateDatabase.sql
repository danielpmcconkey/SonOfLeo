/*
 executed manually in dev 6/13 14:53
 executed manually in test 6/14 10:03
  executed manually in prod 6/14 10:04
 
 
 drop database sonofleo_dev;
 
 */

CREATE DATABASE sonofleo_dev
    WITH
    OWNER = postgres
    ENCODING = 'UTF8' -- REQ-DAL-3.4
    LC_COLLATE = 'en_US.UTF-8' -- REQ-DAL-3.5
    LOCALE_PROVIDER = 'libc'
    CONNECTION LIMIT = -1
    IS_TEMPLATE = False;