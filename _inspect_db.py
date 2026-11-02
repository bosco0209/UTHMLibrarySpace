import sqlite3
c = sqlite3.connect(r'C:\Users\user\source\repos\UTHMLibrarySpace\UTHMLibrarySpace\librarysp.db')
for row in c.execute('select Id, Code, Name, Type, Capacity, CapacityMin, Units, Location, substr(ImageUrl,1,45) from Facilities order by Type, Code'):
    print(row)
