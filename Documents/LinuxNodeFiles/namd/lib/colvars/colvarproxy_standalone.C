#include "colvarmodule.h"
#include "colvaratoms.h"
#include "colvarproxy.h"
#include "colvarproxy_standalone.h"




colvarproxy_standalone::colvarproxy_standalone (std::string const &config_filename,
                                                std::string const &input_prefix_in,
                                                std::string const &output_prefix_in)
{
  input_prefix_str = input_prefix_in;
  if (input_prefix_str.rfind (".colvars.state") != std::string::npos) {
    // strip the extension, if present
    input_prefix_str.erase (input_prefix_str.rfind (".colvars.state"),
                            std::string (".colvars.state").size());
  }

  output_prefix_str = restart_output_prefix_str = output_prefix_in;
  colvars = new colvarmodule (config_filename.c_str(), this);
  colvars->b_analysis = true;
}


colvarproxy_standalone::~colvarproxy_standalone()
{
  delete colvars;
}


cvm::atom::atom (cvm::residue_id const &residue,
                 std::string const     &atom_name,
                 std::string const     &segment_id)
  : id (0), mass (1.0)
{
  reset_data();
}


cvm::atom::atom (int const &atom_number)
  : id (atom_number), mass (1.0)
{
  reset_data();
}


// copy constructor
cvm::atom::atom (cvm::atom const &a)
  : id (a.id), mass (a.mass)
{
  // init_namd_atom() has already been called by a's constructor, no
  // need to call it again
  reset_data();
}


cvm::atom::~atom()
{}


void cvm::atom::read_position()
{
  cvm::fatal_error ("Error: atom data are unavailable in standalone mode.\n");
}

void cvm::atom::read_velocity()
{
  cvm::fatal_error ("Error: atom data are unavailable in standalone mode.\n");
}

void cvm::atom::read_system_force()
{
  cvm::fatal_error ("Error: atom data are unavailable in standalone mode.\n");
}

void cvm::atom::apply_force (cvm::rvector const &new_force)
{
  cvm::fatal_error ("Error: atom data are unavailable in standalone mode.\n");
}

