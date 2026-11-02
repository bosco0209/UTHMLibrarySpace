import sqlite3
con = sqlite3.connect(r'C:\Users\user\source\repos\UTHMLibrarySpace\UTHMLibrarySpace\librarysp.db')
con.execute("UPDATE Users SET Phone='012-3456789', Faculty='FSKTM' WHERE Id=8")
con.commit()
print(con.execute('select Name, Phone, Faculty from Users where Id=8').fetchone())
con.close()
