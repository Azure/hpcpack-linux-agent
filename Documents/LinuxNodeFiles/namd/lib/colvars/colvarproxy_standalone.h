#ifndef COLVARPROXY_STANDALONE_H
#define COLVARPROXY_STANDALONE_H


#include <iostream>
//#include <unistd>

#include "colvarmodule.h"
#include "colvaratoms.h"
#include "colvarproxy.h"


/// \brief Interface class between the collective variables module and
/// the simulation program

class colvarproxy_standalone : public colvarproxy {

protected:

  std::string input_prefix_str, output_prefix_str, restart_output_prefix_str;
  size_t restart_frequency_int;

public:

  /// Constructor
  colvarproxy_standalone (std::string const &config_filename,
                          std::string const &input_prefix_in,
                          std::string const &output_prefix_in);
  /// Destructor
  virtual ~colvarproxy_standalone();

  virtual inline cvm::real unit_angstrom()
  {
    return 1.0;
  }

  virtual cvm::real boltzmann()
  {
    return 0.001987191;
  }

  virtual cvm::real temperature()
  {
    return 300.0;
  }

  virtual inline void log (std::string const &message)
  {
    std::cout << message;
  }

  virtual inline void fatal_error (std::string const &message)
  {
    cvm::log (message);
    if (!cvm::debug())
      cvm::log ("If this error message is unclear, "
                "try recompile with -DCOLVARS_DEBUG.\n");
    ::exit (1);
  }

  virtual inline void exit (std::string const &message)
  {
    cvm::log (message);
    ::exit (0);
  }

  virtual inline std::string input_prefix()
  {
    return input_prefix_str;
  }

  virtual inline std::string restart_output_prefix()
  {
    return restart_output_prefix_str;
  }

  virtual inline std::string output_prefix()
  {
    return output_prefix_str;
  }

  virtual inline size_t restart_frequency()
  {
    return restart_frequency_int;
  }


  virtual inline cvm::rvector position_distance (cvm::atom_pos const &pos1,
                                                 cvm::atom_pos const &pos2)
  {
    return pos2-pos1;
  }

  virtual inline cvm::real position_dist2 (cvm::atom_pos const &pos1,
                                           cvm::atom_pos const &pos2)
  {
    return (pos1-pos2).norm2();
  }

  virtual inline cvm::rvector position_dist2_lgrad (cvm::atom_pos const &pos1,
                                                    cvm::atom_pos const &pos2)
  {
    return 2.0 * (pos1-pos2);
  }

  virtual inline void select_closest_image (cvm::atom_pos &pos,
                                            cvm::atom_pos const &ref_pos)
  {
    return;
  }



  virtual inline void load_coords (char const *filename,
                                   std::vector<cvm::atom_pos> &pos,
                                   std::string const pdb_field = "O",
                                   double const pdb_field_value = 0.0)
  {
    return;
  }


  virtual inline void load_atoms (char const *filename,
                                  std::vector<cvm::atom> &atoms,
                                  std::string const pdb_field = "O",
                                  double const pdb_field_value = 0.0)
  {
    return;
  }

};



#endif


// Emacs
// Local Variables:
// mode: C++
// End:
